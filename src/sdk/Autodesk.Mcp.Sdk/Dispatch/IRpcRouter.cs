using Autodesk.Mcp.Shared.Contracts;

namespace Autodesk.Mcp.Sdk.Dispatch;

/// <summary>Routes incoming request envelopes to protocol handlers and returns responses.</summary>
public interface IRpcRouter
{
    /// <summary>Handles one request envelope; returns null for notifications.</summary>
    /// <param name="request">The parsed request envelope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ResponseEnvelope?> HandleAsync(RequestEnvelope request, CancellationToken cancellationToken);
}
