namespace Civil3D.Tools.CutFill.Dtos;

/// <summary>
/// The <c>calculate_cut_fill</c> result: a structured, read-only earthwork volume report for two
/// Civil 3D surfaces. Combines the volume summary (computed or not-supported), the surface
/// differences, optional derived statistics and optional recommendations. Immutable and
/// Autodesk-free.
/// </summary>
public sealed record CutFillReport
{
    /// <summary>The headline volume summary.</summary>
    public VolumeSummary Summary { get; init; } = new();

    /// <summary>Per-metric surface differences that contextualise the volumes.</summary>
    public IReadOnlyList<VolumeDifference> Differences { get; init; } = Array.Empty<VolumeDifference>();

    /// <summary>Derived statistics; null when not supported or disabled.</summary>
    public VolumeStatistics? Statistics { get; init; }

    /// <summary>Recommendations; empty when not supported or disabled.</summary>
    public IReadOnlyList<CutFillRecommendation> Recommendations { get; init; } = Array.Empty<CutFillRecommendation>();

    /// <summary>Timing and step accounting for the workflow run.</summary>
    public WorkflowExecutionSummary Execution { get; init; } = new();
}
