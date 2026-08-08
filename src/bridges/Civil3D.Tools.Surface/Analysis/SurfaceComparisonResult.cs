using Civil3D.Tools.Surface.Dtos;

namespace Civil3D.Tools.Surface.Analysis;

/// <summary>
/// The comparison-engine output: the headline summary, per-metric comparisons, the differences,
/// optional statistics and optional recommendations. Immutable; produced by
/// <see cref="SurfaceComparer.Compare"/>.
/// </summary>
public sealed record SurfaceComparisonResult(
    SurfaceComparisonSummary Summary,
    IReadOnlyList<SurfaceMetricComparison> Metrics,
    IReadOnlyList<SurfaceDifference> Differences,
    SurfaceComparisonStatistics? Statistics,
    IReadOnlyList<ComparisonRecommendation> Recommendations);
