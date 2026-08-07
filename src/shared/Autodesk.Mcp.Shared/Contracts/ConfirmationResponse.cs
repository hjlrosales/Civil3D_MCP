using System.ComponentModel.DataAnnotations;

namespace Autodesk.Mcp.Shared.Contracts;

/// <summary>
/// The answer to a <see cref="ConfirmationRequest"/>. The <see cref="RequestId"/> binds the response
/// to the originating prompt; a denied confirmation carries an optional reason.
/// </summary>
public sealed record ConfirmationResponse
{
    /// <summary>Correlation identifier of the operation the confirmation belonged to.</summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>The request id of the prompt being answered.</summary>
    [Required]
    public string RequestId { get; init; } = string.Empty;

    /// <summary>True when the user confirmed the operation.</summary>
    public bool Confirmed { get; init; }

    /// <summary>Optional human-readable reason, typically present when confirmation was denied.</summary>
    public string? Reason { get; init; }

    /// <summary>UTC timestamp at which the answer was given.</summary>
    public DateTimeOffset ConfirmedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
