using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using Autodesk.Mcp.Sdk.Dispatch;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Dtos;
using Autodesk.Mcp.Shared.Errors;
using Autodesk.Mcp.Shared.Serialization;
using Microsoft.Extensions.Logging;

namespace Civil3D.Bridge.Execution;

/// <summary>
/// FIFO dispatcher implementing <see cref="IToolExecutor"/>. Callers enqueue a work item and await
/// its completion; a single worker executes items strictly in order (Autodesk APIs are
/// single-threaded, and the dispatcher serializes them). Each item is registered in the
/// <see cref="CancellationRegistry"/> so <c>$/cancel</c> can abort it, and a per-item timeout
/// (manifest value or request override) cancels the work when exceeded. Tools that require the
/// application context are marshaled through <see cref="IApplicationContext"/>; all others run
/// directly on the worker. Graceful shutdown drains queued work and cancels in-flight work.
/// </summary>
public sealed class ToolDispatcher : IToolExecutor, IAsyncDisposable
{
    private sealed record WorkItem(
        ToolInvocation Invocation,
        TaskCompletionSource<ResponseEnvelope> Completion,
        CancellationTokenSource ItemCts,
        CancellationToken EffectiveToken,
        CancellationToken CallerToken);

    private readonly IToolCatalog _catalog;
    private readonly IApplicationContext _applicationContext;
    private readonly CancellationRegistry _cancellations;
    private readonly ILogger<ToolDispatcher> _logger;
    private readonly Channel<WorkItem> _queue;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _startSync = new();
    private Task? _worker;
    private bool _started;
    private bool _stopped;

    /// <summary>Creates the dispatcher.</summary>
    /// <param name="catalog">Tool catalog used to resolve tools and manifests.</param>
    /// <param name="applicationContext">Application-context marshaler for Autodesk-touching tools.</param>
    /// <param name="cancellations">Cancellation registry backing <c>$/cancel</c>.</param>
    /// <param name="logger">Logger.</param>
    public ToolDispatcher(
        IToolCatalog catalog,
        IApplicationContext applicationContext,
        CancellationRegistry cancellations,
        ILogger<ToolDispatcher> logger)
    {
        _catalog = catalog;
        _applicationContext = applicationContext;
        _cancellations = cancellations;
        _logger = logger;
        _queue = Channel.CreateUnbounded<WorkItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <summary>Starts the worker loop. Idempotent and thread-safe; also started lazily on first execution.</summary>
    public void Start()
    {
        lock (_startSync)
        {
            if (_started)
            {
                return;
            }

            _worker = Task.Run(() => WorkerLoopAsync(_shutdown.Token));
            _started = true;
            _logger.LogInformation("Tool dispatcher started (FIFO queue).");
        }
    }

    /// <inheritdoc />
    public async Task<ResponseEnvelope> ExecuteAsync(ToolInvocation invocation, CancellationToken cancellationToken)
    {
        if (_stopped)
        {
            return ResponseEnvelope.Fail(ErrorCode.E_BRIDGE_UNAVAILABLE, "The bridge is shutting down.", invocation.CorrelationId, invocation.SessionId);
        }

        if (!_started)
        {
            Start();
        }

        ToolManifest? manifest = _catalog.GetManifest(invocation.ToolName);
        long timeoutMs = invocation.TimeoutMilliseconds
            ?? manifest?.TimeoutMilliseconds
            ?? ProtocolConstants.DefaultToolTimeoutMilliseconds;

        using var itemCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        itemCts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));

        string correlationId = string.IsNullOrWhiteSpace(invocation.CorrelationId)
            ? Guid.NewGuid().ToString("D")
            : invocation.CorrelationId;
        CancellationToken effective = _cancellations.Register(correlationId, itemCts.Token);
        var completion = new TaskCompletionSource<ResponseEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        var item = new WorkItem(invocation, completion, itemCts, effective, cancellationToken);

        try
        {
            await _queue.Writer.WriteAsync(item, effective);
            ResponseEnvelope response = await completion.Task.WaitAsync(effective);
            return response;
        }
        catch (OperationCanceledException) when (itemCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return ResponseEnvelope.Fail(ErrorCode.E_TIMEOUT, $"Tool '{invocation.ToolName}' timed out after {timeoutMs} ms.", correlationId, invocation.SessionId);
        }
        catch (OperationCanceledException)
        {
            return ResponseEnvelope.Fail(ErrorCode.E_CANCELLED, "The operation was cancelled.", correlationId, invocation.SessionId);
        }
        finally
        {
            _cancellations.Remove(correlationId);
        }
    }

    private async Task<ResponseEnvelope> ExecuteOneAsync(WorkItem item)
    {
        ToolInvocation invocation = item.Invocation;
        string correlationId = string.IsNullOrWhiteSpace(invocation.CorrelationId)
            ? Guid.NewGuid().ToString("D")
            : invocation.CorrelationId;
        var timer = Stopwatch.StartNew();
        try
        {
            if (!_catalog.TryGetTool(invocation.ToolName, out ITool? tool))
            {
                return ResponseEnvelope.Fail(ErrorCode.E_OBJECT_NOT_FOUND, $"Unknown tool '{invocation.ToolName}'.", correlationId, invocation.SessionId);
            }

            var context = new ToolExecutionContext
            {
                ToolName = invocation.ToolName,
                CorrelationId = correlationId,
                SessionId = invocation.SessionId,
                // The registry-backed token: fires on timeout, caller cancellation and $/cancel.
                CancellationToken = item.EffectiveToken,
                Logger = _logger,
            };

            object? result = tool.RequiresApplicationContext
                ? await _applicationContext.ExecuteAsync(() => tool.ExecuteAsync(context, invocation.Parameters), item.EffectiveToken)
                : await tool.ExecuteAsync(context, invocation.Parameters);

            timer.Stop();
            _logger.LogInformation(
                "Tool {Tool} completed in {ExecutionTime} ms (correlation {CorrelationId}, session {SessionId}).",
                invocation.ToolName, timer.ElapsedMilliseconds, correlationId, invocation.SessionId);

            return ResponseEnvelope.Ok(
                data: result is null ? null : JsonSerializer.SerializeToElement(result, SharedJson.Options),
                correlationId: correlationId,
                sessionId: invocation.SessionId,
                executionTime: timer.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (item.ItemCts.IsCancellationRequested && !item.CallerToken.IsCancellationRequested)
        {
            return ResponseEnvelope.Fail(ErrorCode.E_TIMEOUT, $"Tool '{invocation.ToolName}' timed out.", correlationId, invocation.SessionId);
        }
        catch (OperationCanceledException)
        {
            return ResponseEnvelope.Fail(ErrorCode.E_CANCELLED, "The operation was cancelled.", correlationId, invocation.SessionId);
        }
        catch (TimeoutException)
        {
            return ResponseEnvelope.Fail(ErrorCode.E_TIMEOUT, $"Tool '{invocation.ToolName}' timed out.", correlationId, invocation.SessionId);
        }
        catch (BridgeException ex)
        {
            timer.Stop();
            _logger.LogWarning("Tool {Tool} failed with {ErrorCode}: {Message} (correlation {CorrelationId}).",
                invocation.ToolName, ex.ErrorCode, ex.Message, correlationId);
            return ResponseEnvelope.Fail(ex.ErrorCode, ex.Message, correlationId, invocation.SessionId, timer.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            timer.Stop();
            _logger.LogError(ex, "Tool {Tool} failed (correlation {CorrelationId}); the exception never crosses the pipe.", invocation.ToolName, correlationId);
            return ResponseEnvelope.Fail(ErrorCode.E_INTERNAL, "An internal error occurred while executing the tool.", correlationId, invocation.SessionId, timer.ElapsedMilliseconds);
        }
    }

    private async Task WorkerLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (WorkItem item in _queue.Reader.ReadAllAsync(cancellationToken))
            {
                ResponseEnvelope response = await ExecuteOneAsync(item);
                item.Completion.TrySetResult(response);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dispatcher worker loop faulted.");
        }
    }

    /// <summary>Cancels in-flight work, drains the queue and stops the worker. Idempotent and thread-safe.</summary>
    public async Task StopAsync()
    {
        lock (_startSync)
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            _stopped = true;
            _shutdown.Cancel();
            _queue.Writer.TryComplete();
        }

        // Fail any items still queued so their callers unblock deterministically.
        while (_queue.Reader.TryRead(out WorkItem? pending))
        {
            pending.Completion.TrySetResult(ResponseEnvelope.Fail(
                ErrorCode.E_CANCELLED, "The bridge is shutting down.",
                pending.Invocation.CorrelationId, pending.Invocation.SessionId));
        }

        if (_worker is not null)
        {
            try
            {
                await _worker;
            }
            catch (OperationCanceledException)
            {
                // Expected.
            }
        }

        _logger.LogInformation("Tool dispatcher stopped.");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync();
}
