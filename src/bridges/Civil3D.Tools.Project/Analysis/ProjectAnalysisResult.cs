using Civil3D.Tools.Project.Dtos;

namespace Civil3D.Tools.Project.Analysis;

/// <summary>
/// The analyzer output: the overview, inventory, reference summary, complexity assessment,
/// statistics and recommendations. Immutable; produced by <see cref="ProjectAnalyzer.Analyze"/>.
/// </summary>
public sealed record ProjectAnalysisResult(
    ProjectOverview Overview,
    ObjectInventory Inventory,
    ReferenceSummary References,
    ComplexityAssessment Complexity,
    ProjectStatistics Statistics,
    IReadOnlyList<ProjectRecommendation> Recommendations);
