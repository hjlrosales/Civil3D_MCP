using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Dtos;

namespace Autodesk.Mcp.Sdk.Tools.Health;

/// <summary>
/// The payload of <c>bridge/getCapabilities</c>: bridge identity, protocol version, advertised
/// capabilities and the full tool manifest list served by this bridge.
/// </summary>
public sealed record GetCapabilitiesResult
{
    /// <summary>Bridge identity and version information.</summary>
    public BridgeInformation Bridge { get; init; } = new();

    /// <summary>The protocol version the bridge currently speaks.</summary>
    public VersionInformation ProtocolVersion { get; init; } = VersionInformation.Empty;

    /// <summary>The capabilities the bridge advertises.</summary>
    public BridgeCapabilities Capabilities { get; init; } = new();

    /// <summary>All tool manifests served by this bridge.</summary>
    public IReadOnlyList<ToolManifest> Tools { get; init; } = Array.Empty<ToolManifest>();
}
