using Civil3D.Domain.Surfaces.Dtos;

namespace Civil3D.Tools.Surface.Analysis;

/// <summary>
/// Immutable snapshot the comparison engine consumes: the two loaded surfaces plus the
/// thresholds. Produced by the workflow's load step and consumed by
/// <see cref="SurfaceComparer"/>. Autodesk-free.
/// </summary>
public sealed record SurfaceComparisonData
{
    /// <summary>The existing (reference) surface.</summary>
    public SurfaceInfo ExistingSurface { get; init; } = new();

    /// <summary>The proposed (candidate) surface.</summary>
    public SurfaceInfo ProposedSurface { get; init; } = new();

    /// <summary>The thresholds to apply; defaults when omitted.</summary>
    public SurfaceComparisonOptions Options { get; init; } = SurfaceComparisonOptions.Default;

    /// <summary>When true, the engine computes the numeric statistics section.</summary>
    public bool IncludeStatistics { get; init; } = true;

    /// <summary>When true, the engine produces recommendations.</summary>
    public bool IncludeRecommendations { get; init; } = true;
}
