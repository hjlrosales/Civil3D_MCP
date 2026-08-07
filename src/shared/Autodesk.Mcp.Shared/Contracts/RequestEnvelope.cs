using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Autodesk.Mcp.Shared.Contracts;

/// <summary>
/// The JSON-RPC 2.0 request envelope used for every bridge method call
/// (<c>handshake</c>, <c>tools/list</c>, <c>tools/execute</c>, <c>health/ping</c>, <c>shutdown</c>).
/// Notifications (no reply expected) are represented with a null <see cref="Id"/>.
/// </summary>
public sealed record RequestEnvelope
{
    /// <summary>The protocol method name; see <see cref="ProtocolConstants"/>.</summary>
    [Required]
    public string Method { get; init; } = string.Empty;

    /// <summary>
    /// The JSON-RPC request id. Omitted (null) for notifications so the wire stays JSON-RPC 2.0
    /// compliant, where a notification carries no <c>id</c> member at all.
    /// </summary>
    public JsonRpcId? Id { get; init; }

    /// <summary>The positional or named parameters for the method, as a raw JSON value.</summary>
    public JsonElement? Params { get; init; }

    /// <summary>End-to-end correlation identifier propagated into logs and responses.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>The session identifier established at handshake, when applicable.</summary>
    public string? SessionId { get; init; }

    /// <summary>Requested execution timeout in milliseconds; the bridge applies its own maximum.</summary>
    [Range(1, int.MaxValue)]
    public long? TimeoutMilliseconds { get; init; }

    /// <summary>UTC timestamp captured by the caller when the request was sent.</summary>
    public DateTimeOffset? ClientRequestedAtUtc { get; init; }

    /// <summary>Convenience factory for a well-formed request.</summary>
    /// <param name="method">The protocol method name.</param>
    /// <param name="id">The JSON-RPC id.</param>
    /// <param name="parameters">Optional raw parameters.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <param name="sessionId">Optional session identifier.</param>
    /// <param name="timeoutMilliseconds">Optional execution timeout in milliseconds.</param>
    public static RequestEnvelope Create(
        string method,
        JsonRpcId? id = null,
        JsonElement? parameters = null,
        string? correlationId = null,
        string? sessionId = null,
        long? timeoutMilliseconds = null)
        => new()
        {
            Method = method,
            Id = id,
            Params = parameters,
            CorrelationId = correlationId,
            SessionId = sessionId,
            TimeoutMilliseconds = timeoutMilliseconds,
            ClientRequestedAtUtc = DateTimeOffset.UtcNow,
        };
}
