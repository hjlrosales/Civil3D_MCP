namespace Autodesk.Mcp.Sdk.Hosting;

/// <summary>
/// Cooperative shutdown signal. The <c>shutdown</c> protocol method requests shutdown; the host
/// waits on <see cref="WaitForShutdownAsync"/> and then performs a graceful stop.
/// </summary>
public sealed class BridgeShutdown : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly TaskCompletionSource _requested = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Token that is cancelled when shutdown is requested.</summary>
    public CancellationToken Token => _cts.Token;

    /// <summary>Completes when shutdown has been requested.</summary>
    public Task WaitForShutdownAsync() => _requested.Task;

    /// <summary>Requests a graceful shutdown (idempotent).</summary>
    public void Request()
    {
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed.
        }

        _requested.TrySetResult();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts.Dispose();
    }
}
