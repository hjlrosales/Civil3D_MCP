using System.Collections.Concurrent;
using System.IO.Pipes;
using Autodesk.Mcp.Sdk.Dispatch;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Errors;
using Microsoft.Extensions.Logging;

namespace Autodesk.Mcp.Sdk.Communication;

/// <summary>
/// Hosts the named-pipe listener (AD-02). Accepts multiple simultaneous connections, routes each
/// request through the <see cref="IRpcRouter"/>, and never touches Autodesk APIs on its threads.
/// The accept loop survives individual connection faults and client disconnects.
/// </summary>
public sealed class NamedPipeServerHost : IAsyncDisposable
{
    private readonly string _pipeName;
    private readonly int _maxConnections;
    private readonly IRpcRouter _router;
    private readonly ILogger<NamedPipeServerHost> _logger;
    private readonly ConcurrentDictionary<Guid, PipeConnection> _connections = new();
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;
    private bool _started;

    /// <summary>Creates the host.</summary>
    /// <param name="pipeName">The named pipe to listen on.</param>
    /// <param name="maxConcurrentConnections">Maximum simultaneous connections.</param>
    /// <param name="router">Request router.</param>
    /// <param name="logger">Logger.</param>
    public NamedPipeServerHost(string pipeName, int maxConcurrentConnections, IRpcRouter router, ILogger<NamedPipeServerHost> logger)
    {
        _pipeName = pipeName;
        _maxConnections = Math.Max(1, maxConcurrentConnections);
        _router = router;
        _logger = logger;
    }

    /// <summary>Starts the accept loop. Idempotent.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
        {
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token), CancellationToken.None);
        _started = true;
        _logger.LogInformation("Pipe host listening on '{PipeName}' (max {Max} connections).", _pipeName, _maxConnections);
        return Task.CompletedTask;
    }

    /// <summary>Stops the listener and closes all connections.</summary>
    public async Task StopAsync()
    {
        if (!_started)
        {
            return;
        }

        _started = false;
        _cts?.Cancel();
        if (_acceptLoop is not null)
        {
            try
            {
                await _acceptLoop;
            }
            catch (OperationCanceledException)
            {
                // Expected.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Accept loop terminated with an error.");
            }
        }

        foreach (PipeConnection connection in _connections.Values)
        {
            await connection.DisposeAsync();
        }

        _connections.Clear();
        _logger.LogInformation("Pipe host stopped.");
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var server = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                _maxConnections,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            try
            {
                await server.WaitForConnectionAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await server.DisposeAsync();
                return;
            }
            catch (Exception ex)
            {
                await server.DisposeAsync();
                _logger.LogWarning(ex, "Failed to accept a pipe connection.");
                continue;
            }

            var connection = new PipeConnection(server);
            _connections[connection.ConnectionId] = connection;
            _ = Task.Run(() => RunConnectionAsync(connection, cancellationToken), CancellationToken.None);
        }
    }

    private async Task RunConnectionAsync(PipeConnection connection, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Connection {ConnectionId} established.", connection.ConnectionId);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                RequestEnvelope? request = await connection.ReceiveAsync(cancellationToken);
                if (request is null)
                {
                    break; // Client closed the pipe.
                }

                _logger.LogInformation(
                    "Received '{Method}' (id {Id}, correlation {CorrelationId}, session {SessionId}).",
                    request.Method, request.Id, request.CorrelationId, request.SessionId);

                ResponseEnvelope? response = await _router.HandleAsync(request, cancellationToken);
                if (response is not null)
                {
                    await connection.SendAsync(response, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Host shutdown.
        }
        catch (ProtocolException ex)
        {
            _logger.LogWarning("Protocol error on connection {ConnectionId}: {Message}", connection.ConnectionId, ex.Message);
            try
            {
                await connection.SendAsync(
                    new ErrorEnvelope { ErrorCode = ErrorCode.E_INVALID_REQUEST, Message = ex.Message, CorrelationId = ex.CorrelationId },
                    CancellationToken.None);
            }
            catch
            {
                // Best-effort; the connection is failing anyway.
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Connection {ConnectionId} faulted.", connection.ConnectionId);
        }
        finally
        {
            _connections.TryRemove(connection.ConnectionId, out _);
            await connection.DisposeAsync();
            _logger.LogInformation("Connection {ConnectionId} closed.", connection.ConnectionId);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync();
}
