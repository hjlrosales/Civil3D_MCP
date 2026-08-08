using System.Collections.Concurrent;

namespace Autodesk.Mcp.Sdk.Dispatch;

/// <summary>
/// Maps correlation identifiers to cancellation sources so <c>$/cancel</c> notifications can abort
/// in-flight tool executions. Thread-safe.
/// </summary>
public sealed class CancellationRegistry
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _sources = new(StringComparer.Ordinal);

    /// <summary>Registers a correlation id and returns a token linked to the given token.</summary>
    /// <param name="correlationId">Correlation identifier of the in-flight operation.</param>
    /// <param name="linked">Token to link (timeout/shutdown).</param>
    public CancellationToken Register(string correlationId, CancellationToken linked)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(linked);
        _sources[correlationId] = cts;
        return cts.Token;
    }

    /// <summary>Cancels the operation registered under a correlation id.</summary>
    public bool Cancel(string correlationId)
    {
        if (_sources.TryGetValue(correlationId, out CancellationTokenSource? cts))
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                return false;
            }

            return true;
        }

        return false;
    }

    /// <summary>Removes and disposes the registration for a correlation id.</summary>
    public void Remove(string correlationId)
    {
        if (_sources.TryRemove(correlationId, out CancellationTokenSource? cts))
        {
            cts.Dispose();
        }
    }
}
