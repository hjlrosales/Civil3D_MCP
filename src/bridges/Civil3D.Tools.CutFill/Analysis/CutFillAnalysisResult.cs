using Civil3D.Tools.CutFill.Dtos;

namespace Civil3D.Tools.CutFill.Analysis;

/// <summary>
/// The analyzer output: the volume summary, contextual surface differences, optional derived
/// statistics and optional recommendations. Immutable; produced by
/// <see cref="CutFillAnalyzer.Analyze"/>.
/// </summary>
public sealed record CutFillAnalysisResult(
    VolumeSummary Summary,
    IReadOnlyList<VolumeDifference> Differences,
    VolumeStatistics? Statistics,
    IReadOnlyList<CutFillRecommendation> Recommendations);
