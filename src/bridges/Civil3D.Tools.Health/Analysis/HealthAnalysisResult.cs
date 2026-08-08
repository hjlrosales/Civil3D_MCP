using Civil3D.Tools.Health.Dtos;

namespace Civil3D.Tools.Health.Analysis;

/// <summary>
/// The analyzer output: findings, per-category roll-ups, top-level recommendations and the
/// severity statistics. Immutable; produced by <see cref="HealthAnalyzer.Analyze"/>.
/// </summary>
/// <param name="Issues">The findings, ordered by severity then code.</param>
/// <param name="Categories">Per-category severity roll-ups.</param>
/// <param name="Recommendations">Top-level recommendations.</param>
/// <param name="Statistics">Severity roll-up plus the inspected object count.</param>
public sealed record HealthAnalysisResult(
    IReadOnlyList<HealthIssue> Issues,
    IReadOnlyList<HealthCategory> Categories,
    IReadOnlyList<HealthRecommendation> Recommendations,
    HealthStatistics Statistics);
