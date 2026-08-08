using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Dtos;

namespace Autodesk.Mcp.Sdk.Hosting;

/// <summary>Static configuration describing the bridge instance (identity, versions, pipe, capabilities).</summary>
public sealed class BridgeHostOptions
{
    /// <summary>Logical bridge name (for example <c>Civil3D.Bridge</c>).</summary>
    public string BridgeName { get; set; } = string.Empty;

    /// <summary>Product identifier (for example <c>Civil3D</c>).</summary>
    public string Product { get; set; } = string.Empty;

    /// <summary>Product version, not necessarily semantic (for example <c>2025</c>).</summary>
    public string? ProductVersion { get; set; }

    /// <summary>Version of the bridge itself.</summary>
    public VersionInformation BridgeVersion { get; set; } = new(1, 0, 0);

    /// <summary>Version of the SDK assembly the bridge is built against.</summary>
    public VersionInformation SdkVersion { get; set; } = new(1, 0, 0);

    /// <summary>Version of the wire protocol the bridge speaks.</summary>
    public VersionInformation ProtocolVersion { get; set; } = ProtocolConstants.CurrentProtocolVersion;

    /// <summary>Named pipe the bridge listens on.</summary>
    public string PipeName { get; set; } = string.Empty;

    /// <summary>Maximum simultaneous pipe connections.</summary>
    public int MaxConcurrentConnections { get; set; } = 8;

    /// <summary>Products this bridge serves (used in discovery and handshake).</summary>
    public string[] SupportedProducts { get; set; } = Array.Empty<string>();

    /// <summary>The capabilities the bridge advertises.</summary>
    public BridgeCapabilities Capabilities { get; set; } = new();
}
