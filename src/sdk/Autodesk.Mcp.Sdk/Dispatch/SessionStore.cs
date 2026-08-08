using System.Collections.Concurrent;
using Autodesk.Mcp.Shared.Contracts;

namespace Autodesk.Mcp.Sdk.Dispatch;

/// <summary>
/// Thread-safe store of active bridge sessions, created at handshake time. Safe for multiple
/// simultaneous pipe clients.
/// </summary>
public sealed class SessionStore
{
    private readonly ConcurrentDictionary<string, SessionInformation> _sessions = new(StringComparer.Ordinal);

    /// <summary>Creates and stores a new session.</summary>
    /// <param name="clientName">Name of the connecting client.</param>
    /// <param name="clientVersion">Version of the connecting client, when known.</param>
    public SessionInformation Create(string? clientName, string? clientVersion)
    {
        var session = new SessionInformation
        {
            SessionId = Guid.NewGuid().ToString("D"),
            ClientName = clientName,
            ClientVersion = clientVersion,
            StartedAtUtc = DateTimeOffset.UtcNow,
        };
        _sessions[session.SessionId] = session;
        return session;
    }

    /// <summary>Gets a session by id.</summary>
    public bool TryGet(string sessionId, out SessionInformation session)
        => _sessions.TryGetValue(sessionId, out session!);

    /// <summary>Removes a session.</summary>
    public void Remove(string sessionId) => _sessions.TryRemove(sessionId, out _);

    /// <summary>Removes all sessions.</summary>
    public void Clear() => _sessions.Clear();

    /// <summary>All active sessions.</summary>
    public IReadOnlyCollection<SessionInformation> All => _sessions.Values.ToArray();
}
