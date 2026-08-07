namespace Autodesk.Mcp.Shared.Contracts;

/// <summary>
/// Metadata describing an established bridge session, created by the bridge at handshake time and
/// echoed back on every subsequent request.
/// </summary>
public sealed record SessionInformation
{
    /// <summary>The stable session identifier issued by the bridge.</summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>The name of the connected client (for example the MCP server).</summary>
    public string? ClientName { get; init; }

    /// <summary>The version of the connected client, when reported.</summary>
    public string? ClientVersion { get; init; }

    /// <summary>UTC timestamp at which the session was established.</summary>
    public DateTimeOffset StartedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp of the most recent activity, if tracked.</summary>
    public DateTimeOffset? LastActivityAtUtc { get; init; }

    /// <summary>UTC timestamp after which the session is considered expired, when applicable.</summary>
    public DateTimeOffset? ExpiresAtUtc { get; init; }
}
