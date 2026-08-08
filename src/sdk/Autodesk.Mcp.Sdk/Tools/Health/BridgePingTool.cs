using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Enums;
using Autodesk.Mcp.Sdk.Registration;

namespace Autodesk.Mcp.Sdk.Tools.Health;

/// <summary>
/// Health tool <c>bridge/ping</c>: returns a liveness heartbeat and refreshes the endpoint
/// descriptor's heartbeat timestamp so the MCP server can detect liveness.
/// </summary>
[McpTool(
    "bridge/ping",
    "Bridge Ping",
    "Returns a liveness heartbeat from the bridge. The bridge refreshes its endpoint descriptor " +
    "heartbeat timestamp, which the MCP server uses to detect liveness.",
    Category = ToolCategory.General,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low)]
public sealed class BridgePingTool : ToolBase<EmptyParameters, Heartbeat>
{
    private readonly IEndpointRegistrar? _registrar;

    /// <summary>Creates the tool.</summary>
    /// <param name="registrar">Optional endpoint registrar refreshed with each ping.</param>
    public BridgePingTool(IEndpointRegistrar? registrar = null)
    {
        _registrar = registrar;
    }

    /// <inheritdoc />
    protected override Task<Heartbeat> ExecuteCoreAsync(EmptyParameters input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (_registrar is not null)
        {
            _ = _registrar.UpdateHeartbeatAsync(now, CancellationToken.None);
        }

        return Task.FromResult(new Heartbeat
        {
            TimestampUtc = now,
            SessionId = context.SessionId,
            ProcessId = Environment.ProcessId,
        });
    }
}
