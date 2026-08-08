using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Tools.Surface.Analysis;
using Civil3D.Tools.Surface.Dtos;

namespace Civil3D.Tools.Surface.Workflow;

/// <summary>
/// Mutable working state shared between the steps of one surface-comparison workflow execution.
/// A fresh instance is created with every workflow; it is never reused across runs, so the
/// mutable collections are safe. Steps write materialized DTOs here; the report step composes
/// them.
/// </summary>
public sealed class SurfaceComparisonWorkflowState
{
    /// <summary>The request driving this execution.</summary>
    public SurfaceComparisonRequest Request { get; set; } = new();

    /// <summary>The existing (reference) surface loaded by the metadata step.</summary>
    public SurfaceInfo? ExistingSurface { get; set; }

    /// <summary>The proposed (candidate) surface loaded by the metadata step.</summary>
    public SurfaceInfo? ProposedSurface { get; set; }

    /// <summary>The immutable comparison snapshot built by the load step.</summary>
    public SurfaceComparisonData? Data { get; set; }

    /// <summary>The comparison output produced by the analysis step.</summary>
    public SurfaceComparisonResult? Result { get; set; }

    /// <summary>The final report produced by the report step.</summary>
    public SurfaceComparisonReport? Report { get; set; }
}
