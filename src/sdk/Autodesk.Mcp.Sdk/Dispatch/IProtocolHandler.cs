using System.Text.Json;

namespace Autodesk.Mcp.Sdk.Dispatch;

/// <summary>
/// Handles one JSON-RPC method of the bridge protocol. Implementations must be thread-safe and
/// must never touch Autodesk APIs directly; Autodesk work flows through the tool executor.
/// </summary>
public interface IProtocolHandler
{
    /// <summary>The protocol method this handler serves (for example <c>handshake</c>).</summary>
    string Method { get; }

    /// <summary>Handles a request and returns the payload to place in the response envelope.</summary>
    /// <param name="parameters">Raw method parameters, or null when absent.</param>
    /// <param name="context">Per-request routing context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<object?> HandleAsync(JsonElement? parameters, RpcContext context, CancellationToken cancellationToken);
}
