namespace Civil3D.Tools.Corridor.Dtos;

/// <summary>
/// The <c>corridor_analysis_report</c> result: a structured, read-only summary and health
/// analysis of one or all corridors. Combines an overall verdict, per-corridor summaries,
/// aggregate statistics, health issues and recommendations. Immutable and Autodesk-free.
/// </summary>
public sealed record CorridorAnalysisReport
{
    /// <summary>An overall verdict, for example <c>Healthy</c> or <c>Attention Required</c>.</summary>
    public string Verdict { get; init; } = string.Empty;

    /// <summary>Every analyzed corridor with its metrics and health status.</summary>
    public IReadOnlyList<CorridorSummary> Corridors { get; init; } = Array.Empty<CorridorSummary>();

    /// <summary>Aggregate statistics; null when disabled.</summary>
    public CorridorStatistics? Statistics { get; init; }

    /// <summary>Per-corridor health issues, ordered by severity.</summary>
    public IReadOnlyList<CorridorIssue> Issues { get; init; } = Array.Empty<CorridorIssue>();

    /// <summary>Recommendations; empty when disabled.</summary>
    public IReadOnlyList<CorridorRecommendation> Recommendations { get; init; } = Array.Empty<CorridorRecommendation>();

    /// <summary>Timing and step accounting for the workflow run.</summary>
    public WorkflowExecutionSummary Execution { get; init; } = new();
}
