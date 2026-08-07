using System.ComponentModel.DataAnnotations;

namespace Autodesk.Mcp.Shared.Contracts;

/// <summary>
/// A notification streamed from the bridge to the MCP server while a long-running tool executes.
/// Sent as the <c>$/progress</c> notification; the correlation identifier links the stream to
/// the originating request.
/// </summary>
public sealed record ProgressNotification
{
    /// <summary>Correlation identifier of the in-flight operation this progress belongs to.</summary>
    [Required]
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>Session identifier of the in-flight operation, when known.</summary>
    public string? SessionId { get; init; }

    /// <summary>The name of the tool that is reporting progress.</summary>
    public string? ToolName { get; init; }

    /// <summary>Completion percentage in the range 0..100.</summary>
    [Range(0, 100)]
    public int Percent { get; init; }

    /// <summary>Short stage label (for example <c>rebuilding corridor</c>).</summary>
    public string? Stage { get; init; }

    /// <summary>Optional human-readable detail for the current stage.</summary>
    public string? Message { get; init; }

    /// <summary>UTC timestamp at which this progress update was produced.</summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
}
