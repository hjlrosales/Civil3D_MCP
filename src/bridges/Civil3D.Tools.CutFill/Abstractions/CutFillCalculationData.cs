using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Tools.CutFill.Analysis;

namespace Civil3D.Tools.CutFill.Abstractions;

/// <summary>
/// Immutable snapshot a <see cref="ICutFillCalculator"/> consumes: the two loaded surfaces plus
/// the analysis thresholds. Produced by the workflow's preparation step; Autodesk-free.
/// </summary>
public sealed record CutFillCalculationData
{
    /// <summary>The existing ground (reference) surface.</summary>
    public SurfaceInfo ExistingSurface { get; init; } = new();

    /// <summary>The proposed (design) surface.</summary>
    public SurfaceInfo ProposedSurface { get; init; } = new();

    /// <summary>The thresholds the analyzer applies; defaults when omitted.</summary>
    public CutFillOptions Options { get; init; } = CutFillOptions.Default;
}
