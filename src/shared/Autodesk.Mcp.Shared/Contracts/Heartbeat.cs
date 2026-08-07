namespace Autodesk.Mcp.Shared.Contracts;

/// <summary>
/// A liveness message exchanged on <c>health/ping</c>. The MCP server uses it to confirm that a
/// bridge is still alive and responsive; the bridge records the timestamp as its last heartbeat.
/// </summary>
public sealed record Heartbeat
{
    /// <summary>UTC timestamp at which the heartbeat was produced.</summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Session identifier of the bridge being pinged, when applicable.</summary>
    public string? SessionId { get; init; }

    /// <summary>The operating system process id of the bridge, when known.</summary>
    public int? ProcessId { get; init; }
}
