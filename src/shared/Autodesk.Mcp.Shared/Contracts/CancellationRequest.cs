using System.ComponentModel.DataAnnotations;

namespace Autodesk.Mcp.Shared.Contracts;

/// <summary>
/// A request to cancel an in-flight tool execution. Sent as the <c>$/cancel</c> notification;
/// the bridge maps it to a cancellation token and aborts the running tool.
/// </summary>
public sealed record CancellationRequest
{
    /// <summary>Correlation identifier of the operation to cancel.</summary>
    [Required]
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>Optional human-readable reason for the cancellation.</summary>
    public string? Reason { get; init; }

    /// <summary>UTC timestamp at which the cancellation was requested.</summary>
    public DateTimeOffset RequestedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
