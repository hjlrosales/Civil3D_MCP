namespace Autodesk.Mcp.Shared.Contracts;

/// <summary>
/// End-to-end correlation metadata propagated through every request/response and every log line.
/// The wire envelope flattens <see cref="CorrelationId"/> and <see cref="SessionId"/> for compatibility;
/// this type is the richer context used by logging and diagnostics.
/// </summary>
public sealed record CorrelationInformation
{
    /// <summary>The unique identifier for this request chain.</summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>The identifier of the upstream operation that spawned this one, if any.</summary>
    public string? ParentCorrelationId { get; init; }

    /// <summary>The session this correlation belongs to, if known.</summary>
    public string? SessionId { get; init; }

    /// <summary>Free-form origin label (for example the component that created the correlation).</summary>
    public string? Source { get; init; }

    /// <summary>UTC timestamp at which the correlation was created.</summary>
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a new correlation with a freshly generated GUID identifier.
    /// </summary>
    /// <param name="sessionId">Optional session identifier to attach.</param>
    /// <param name="parentCorrelationId">Optional parent correlation identifier.</param>
    /// <param name="source">Optional origin label.</param>
    public static CorrelationInformation New(string? sessionId = null, string? parentCorrelationId = null, string? source = null)
        => new()
        {
            CorrelationId = Guid.NewGuid().ToString("D"),
            ParentCorrelationId = parentCorrelationId,
            SessionId = sessionId,
            Source = source,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
}
