using Civil3D.Tools.Corridor.Dtos;

namespace Civil3D.Tools.Corridor.Analysis;

/// <summary>
/// The analyzer output: the overall verdict, per-corridor summaries, aggregate statistics,
/// health issues and recommendations. Immutable; produced by
/// <see cref="CorridorAnalyzer.Analyze"/> and <see cref="CorridorAnalyzer.BuildRecommendations"/>.
/// </summary>
public sealed record CorridorAnalysisResult
{
    /// <summary>The overall verdict, for example <c>Healthy</c> or <c>Attention Required</c>.</summary>
    public string Verdict { get; init; } = string.Empty;

    /// <summary>Every analyzed corridor with its metrics and health status.</summary>
    public IReadOnlyList<CorridorSummary> Corridors { get; init; } = Array.Empty<CorridorSummary>();

    /// <summary>Aggregate statistics; null when disabled.</summary>
    public CorridorStatistics? Statistics { get; init; }

    /// <summary>Per-corridor health issues, ordered by severity.</summary>
    public IReadOnlyList<CorridorIssue> Issues { get; init; } = Array.Empty<CorridorIssue>();

    /// <summary>Recommendations; empty when disabled.</summary>
    public IReadOnlyList<CorridorRecommendation> Recommendations { get; init; } = Array.Empty<CorridorRecommendation>();
}
