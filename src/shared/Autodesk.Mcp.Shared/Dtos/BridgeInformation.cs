using System.ComponentModel.DataAnnotations;
using Autodesk.Mcp.Shared.Contracts;

namespace Autodesk.Mcp.Shared.Dtos;

/// <summary>
/// Descriptive metadata a bridge reports about itself during the handshake.
/// This is the runtime view; the <see cref="EndpointDescriptor"/> is the on-disk discovery view.
/// </summary>
public sealed record BridgeInformation
{
    /// <summary>Logical bridge name (for example <c>Civil3D.Bridge</c>).</summary>
    [Required]
    public string BridgeName { get; init; } = string.Empty;

    /// <summary>Product identifier (for example <c>Civil3D</c>).</summary>
    [Required]
    public string Product { get; init; } = string.Empty;

    /// <summary>Product version, not necessarily semantic (for example <c>2026</c>).</summary>
    public string? ProductVersion { get; init; }

    /// <summary>Version of the bridge itself.</summary>
    public VersionInformation BridgeVersion { get; init; } = VersionInformation.Empty;

    /// <summary>Version of the SDK assembly the bridge is built against.</summary>
    public VersionInformation SdkVersion { get; init; } = VersionInformation.Empty;

    /// <summary>Version of the wire protocol the bridge speaks.</summary>
    public VersionInformation ProtocolVersion { get; init; } = VersionInformation.Empty;

    /// <summary>The capabilities the bridge offers.</summary>
    public BridgeCapabilities? Capabilities { get; init; }
}
