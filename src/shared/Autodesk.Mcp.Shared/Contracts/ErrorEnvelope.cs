using System.Text.Json;
using Autodesk.Mcp.Shared.Errors;

namespace Autodesk.Mcp.Shared.Contracts;

/// <summary>
/// The structured error object used when a JSON-RPC response carries a protocol-level error
/// (for example malformed JSON, unknown method, or transport failure). This complements
/// <see cref="ResponseEnvelope"/>: application-level tool failures use the standard envelope,
/// while protocol-level failures use this type.
/// </summary>
public sealed record ErrorEnvelope
{
    /// <summary>The stable error code; see <see cref="Errors.ErrorCode"/>.</summary>
    public ErrorCode ErrorCode { get; init; } = ErrorCode.E_UNKNOWN;

    /// <summary>A safe, user-visible message. Never contains exception details or stack traces.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Optional structured details that help diagnose the failure.</summary>
    public JsonElement? Details { get; init; }

    /// <summary>Correlation identifier of the failing request, when known.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Session identifier of the failing request, when known.</summary>
    public string? SessionId { get; init; }

    /// <summary>UTC timestamp at which the error was produced.</summary>
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
