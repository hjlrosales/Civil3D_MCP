namespace Civil3D.Tools.Surface.Dtos;

/// <summary>
/// The <c>surface_comparison_report</c> result: a structured, read-only comparison of two Civil
/// 3D surfaces using only the metrics the domain layer exposes. Combines the headline summary,
/// per-metric comparisons, the differences, optional numeric statistics and optional
/// recommendations. Immutable and Autodesk-free.
/// </summary>
public sealed record SurfaceComparisonReport
{
    /// <summary>The headline summary of the comparison.</summary>
    public SurfaceComparisonSummary Summary { get; init; } = new();

    /// <summary>Every compared metric, existing vs proposed.</summary>
    public IReadOnlyList<SurfaceMetricComparison> Metrics { get; init; } = Array.Empty<SurfaceMetricComparison>();

    /// <summary>The metrics that differ, ordered by severity.</summary>
    public IReadOnlyList<SurfaceDifference> Differences { get; init; } = Array.Empty<SurfaceDifference>();

    /// <summary>Numeric deltas; null when the request disabled statistics.</summary>
    public SurfaceComparisonStatistics? Statistics { get; init; }

    /// <summary>Recommendations; empty when the request disabled them.</summary>
    public IReadOnlyList<ComparisonRecommendation> Recommendations { get; init; } = Array.Empty<ComparisonRecommendation>();

    /// <summary>Timing and step accounting for the workflow run.</summary>
    public WorkflowExecutionSummary Execution { get; init; } = new();
}
