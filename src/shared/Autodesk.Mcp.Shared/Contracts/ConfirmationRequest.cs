using System.ComponentModel.DataAnnotations;
using Autodesk.Mcp.Shared.Enums;

namespace Autodesk.Mcp.Shared.Contracts;

/// <summary>
/// A confirmation prompt raised before a potentially destructive or high-risk operation is executed.
/// It is raised by the MCP server against the AI client (elicitation) when the client supports it;
/// the same shape is shared so both sides can implement and test the flow against one contract.
/// </summary>
public sealed record ConfirmationRequest
{
    /// <summary>Correlation identifier of the operation awaiting confirmation.</summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>Stable identifier for this confirmation prompt, echoed in the response.</summary>
    [Required]
    public string RequestId { get; init; } = string.Empty;

    /// <summary>Short human-readable title describing the pending action.</summary>
    [Required]
    public string Title { get; init; } = string.Empty;

    /// <summary>Human-readable description of what will happen if confirmed.</summary>
    [Required]
    public string Message { get; init; } = string.Empty;

    /// <summary>The risk level of the pending operation.</summary>
    public ToolRisk Risk { get; init; } = ToolRisk.Unknown;

    /// <summary>Optional structured summary of the pending change (for example object counts).</summary>
    public string? OperationSummary { get; init; }

    /// <summary>Optional response options beyond the default confirm/deny pair.</summary>
    public IReadOnlyList<string> Options { get; init; } = Array.Empty<string>();

    /// <summary>Seconds after which the prompt expires and is treated as denied.</summary>
    [Range(1, int.MaxValue)]
    public int TimeoutSeconds { get; init; } = 60;
}
