using System.Text.Json;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Sdk.Registration;

namespace Autodesk.Mcp.Sdk.Dispatch;

/// <summary>
/// Handles <c>health/ping</c>: returns a heartbeat and refreshes the endpoint descriptor's
/// heartbeat timestamp so the MCP server can detect liveness.
/// </summary>
public sealed class PingHandler : IProtocolHandler
{
    private readonly IEndpointRegistrar? _registrar;

    /// <summary>Creates the handler.</summary>
    /// <param name="registrar">Optional endpoint registrar used to refresh the heartbeat file.</param>
    public PingHandler(IEndpointRegistrar? registrar = null)
    {
        _registrar = registrar;
    }

    /// <inheritdoc />
    public string Method => ProtocolConstants.HealthPing;

    /// <inheritdoc />
    public Task<object?> HandleAsync(JsonElement? parameters, RpcContext context, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var heartbeat = new Heartbeat
        {
            TimestampUtc = now,
            SessionId = context.SessionId,
            ProcessId = Environment.ProcessId,
        };

        if (_registrar is not null)
        {
            _ = _registrar.UpdateHeartbeatAsync(now, CancellationToken.None);
        }

        return Task.FromResult<object?>(heartbeat);
    }
}
