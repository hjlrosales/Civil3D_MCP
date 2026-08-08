using System.Diagnostics;
using System.Text.Json;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Errors;
using Autodesk.Mcp.Shared.Serialization;
using Microsoft.Extensions.Logging;

namespace Autodesk.Mcp.Sdk.Dispatch;

/// <summary>
/// Routes request envelopes to protocol handlers and maps every failure to a stable error code.
/// Raw exceptions never escape: they are converted to the standard response envelope.
/// </summary>
public sealed class JsonRpcRouter : IRpcRouter
{
    private readonly IReadOnlyDictionary<string, IProtocolHandler> _handlers;
    private readonly CancellationRegistry _cancellations;
    private readonly ILogger<JsonRpcRouter> _logger;

    /// <summary>Creates the router from the registered handlers.</summary>
    /// <param name="handlers">All protocol handlers; keys are their method names.</param>
    /// <param name="cancellations">Cancellation registry for <c>$/cancel</c>.</param>
    /// <param name="logger">Logger.</param>
    public JsonRpcRouter(
        IEnumerable<IProtocolHandler> handlers,
        CancellationRegistry cancellations,
        ILogger<JsonRpcRouter> logger)
    {
        _cancellations = cancellations;
        _logger = logger;
        _handlers = handlers.ToDictionary(static h => h.Method, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public async Task<ResponseEnvelope?> HandleAsync(RequestEnvelope request, CancellationToken cancellationToken)
    {
        var context = new RpcContext
        {
            RequestId = request.Id,
            CorrelationId = request.CorrelationId,
            SessionId = request.SessionId,
        };

        if (string.IsNullOrWhiteSpace(request.Method))
        {
            return Failure(ErrorCode.E_INVALID_REQUEST, "A JSON-RPC request requires a 'method'.", context);
        }

        if (request.Method == ProtocolConstants.CancelNotification)
        {
            HandleCancelNotification(request);
            return null;
        }

        if (!_handlers.TryGetValue(request.Method, out IProtocolHandler? handler))
        {
            return context.IsNotification
                ? null
                : Failure(ErrorCode.E_INVALID_REQUEST, $"Unknown method '{request.Method}'.", context);
        }

        var timer = Stopwatch.StartNew();
        try
        {
            object? result = await handler.HandleAsync(request.Params, context, cancellationToken);
            timer.Stop();

            return result switch
            {
                ResponseEnvelope envelope => envelope with
                {
                    ExecutionTime = envelope.ExecutionTime > 0 ? envelope.ExecutionTime : timer.ElapsedMilliseconds,
                },
                null when context.IsNotification => null,
                null => ResponseEnvelope.Ok(correlationId: context.CorrelationId, sessionId: context.SessionId, executionTime: timer.ElapsedMilliseconds),
                _ => ResponseEnvelope.Ok(
                    data: JsonSerializer.SerializeToElement(result, SharedJson.Options),
                    correlationId: context.CorrelationId,
                    sessionId: context.SessionId,
                    executionTime: timer.ElapsedMilliseconds),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null; // Host or connection is shutting down; no response expected.
        }
        catch (OperationCanceledException)
        {
            return Failure(ErrorCode.E_CANCELLED, "The operation was cancelled.", context, timer);
        }
        catch (TimeoutException)
        {
            return Failure(ErrorCode.E_TIMEOUT, "The operation timed out.", context, timer);
        }
        catch (BridgeException ex)
        {
            return Failure(ex.ErrorCode, ex.Message, context, timer);
        }
        catch (JsonException)
        {
            return Failure(ErrorCode.E_SERIALIZATION, "The request payload could not be deserialized.", context, timer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error handling '{Method}' (correlation {CorrelationId}).",
                request.Method, request.CorrelationId);
            return Failure(ErrorCode.E_INTERNAL, "An internal error occurred.", context, timer);
        }
    }

    private void HandleCancelNotification(RequestEnvelope request)
    {
        if (request.Params is not { ValueKind: JsonValueKind.Object })
        {
            return;
        }

        CancellationRequest? cancel = request.Params.Value.Deserialize<CancellationRequest>(SharedJson.Options);
        if (cancel is not null && !string.IsNullOrWhiteSpace(cancel.CorrelationId))
        {
            _logger.LogInformation("Cancellation requested for correlation {CorrelationId}.", cancel.CorrelationId);
            _cancellations.Cancel(cancel.CorrelationId);
        }
    }

    private static ResponseEnvelope Failure(ErrorCode code, string message, RpcContext context, Stopwatch? timer = null)
        => ResponseEnvelope.Fail(
            code,
            message,
            correlationId: context.CorrelationId,
            sessionId: context.SessionId,
            executionTime: timer?.ElapsedMilliseconds ?? 0);
}
