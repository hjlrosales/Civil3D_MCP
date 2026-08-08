using Autodesk.Mcp.Shared.Dtos;

namespace Autodesk.Mcp.Sdk.Registration;

/// <summary>
/// Manages the on-disk endpoint descriptor that lets the MCP server discover live bridges
/// (AD-03). The descriptor is written at startup, refreshed on heartbeat, and deleted on shutdown.
/// </summary>
public interface IEndpointRegistrar
{
    /// <summary>Writes the endpoint descriptor file.</summary>
    Task RegisterAsync(EndpointDescriptor descriptor, CancellationToken cancellationToken = default);

    /// <summary>Rewrites the descriptor with a fresh heartbeat timestamp.</summary>
    Task UpdateHeartbeatAsync(DateTimeOffset timestamp, CancellationToken cancellationToken = default);

    /// <summary>Deletes the endpoint descriptor file.</summary>
    Task DeleteAsync();
}
