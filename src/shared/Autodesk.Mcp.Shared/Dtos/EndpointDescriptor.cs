using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Autodesk.Mcp.Shared.Contracts;

namespace Autodesk.Mcp.Shared.Dtos;

/// <summary>
/// The discovery record a bridge writes to <c>%LOCALAPPDATA%\AutodeskMcp\endpoints\&lt;product&gt;-&lt;pid&gt;.json</c>
/// on startup and deletes on clean shutdown. The MCP server scans this directory, checks that the
/// <see cref="ProcessId"/> is alive, and connects to the pipe. Stale entries are ignored.
/// </summary>
public sealed record EndpointDescriptor
{
    /// <summary>Logical bridge name (for example <c>Civil3D.Bridge</c>).</summary>
    [Required]
    public string BridgeName { get; init; } = string.Empty;

    /// <summary>Product identifier (for example <c>Civil3D</c>).</summary>
    [Required]
    public string Product { get; init; } = string.Empty;

    /// <summary>Product version, not necessarily semantic (for example <c>2026</c>).</summary>
    public string? ProductVersion { get; init; }

    /// <summary>Version of the bridge that owns this endpoint.</summary>
    public VersionInformation BridgeVersion { get; init; } = VersionInformation.Empty;

    /// <summary>Version of the SDK assembly the bridge is built against.</summary>
    public VersionInformation SdkVersion { get; init; } = VersionInformation.Empty;

    /// <summary>Version of the wire protocol the bridge speaks.</summary>
    public VersionInformation ProtocolVersion { get; init; } = VersionInformation.Empty;

    /// <summary>The named pipe the bridge is listening on.</summary>
    [Required]
    public string PipeName { get; init; } = string.Empty;

    /// <summary>The operating system process id of the bridge (wire name <c>pid</c>, matching AD-03).</summary>
    [JsonPropertyName("pid")]
    public int ProcessId { get; init; }

    /// <summary>UTC timestamp of bridge startup (wire name <c>startedUtc</c>, matching AD-03).</summary>
    [JsonPropertyName("startedUtc")]
    public DateTimeOffset StartedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp of the most recent heartbeat, when the bridge reports them.</summary>
    public DateTimeOffset? LastHeartbeatAtUtc { get; init; }

    /// <summary>The capabilities the bridge offers, mirrored here so the server can pre-filter.</summary>
    public BridgeCapabilities? Capabilities { get; init; }
}
