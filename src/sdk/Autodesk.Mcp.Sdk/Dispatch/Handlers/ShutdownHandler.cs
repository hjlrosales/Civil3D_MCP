using System.Text.Json;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Sdk.Hosting;

namespace Autodesk.Mcp.Sdk.Dispatch;

/// <summary>
/// Handles <c>shutdown</c>: requests a graceful host shutdown. The response is best-effort;
/// the host stops shortly after.
/// </summary>
public sealed class ShutdownHandler : IProtocolHandler
{
    private readonly BridgeShutdown _shutdown;

    /// <summary>Creates the handler.</summary>
    public ShutdownHandler(BridgeShutdown shutdown)
    {
        _shutdown = shutdown;
    }

    /// <inheritdoc />
    public string Method => ProtocolConstants.Shutdown;

    /// <inheritdoc />
    public Task<object?> HandleAsync(JsonElement? parameters, RpcContext context, CancellationToken cancellationToken)
    {
        _shutdown.Request();
        return Task.FromResult<object?>("Shutdown requested.");
    }
}
