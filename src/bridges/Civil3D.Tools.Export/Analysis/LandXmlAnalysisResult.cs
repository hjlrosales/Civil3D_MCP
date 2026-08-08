using Civil3D.Tools.Export.Dtos;

namespace Civil3D.Tools.Export.Analysis;

/// <summary>
/// The analyzer output: the export summary, per-type statistics and recommendations. Immutable;
/// produced by <see cref="LandXmlExportAnalyzer.Analyze"/>.
/// </summary>
public sealed record LandXmlAnalysisResult
{
    /// <summary>The headline export summary.</summary>
    public ExportSummary Summary { get; init; } = new();

    /// <summary>Per-type object counts and export totals.</summary>
    public ExportStatistics Statistics { get; init; } = new();

    /// <summary>Recommendations derived from the outcome.</summary>
    public IReadOnlyList<ExportRecommendation> Recommendations { get; init; } = Array.Empty<ExportRecommendation>();
}
