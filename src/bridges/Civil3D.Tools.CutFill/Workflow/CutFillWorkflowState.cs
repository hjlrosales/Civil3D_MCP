using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Tools.CutFill.Abstractions;
using Civil3D.Tools.CutFill.Analysis;
using Civil3D.Tools.CutFill.Dtos;

namespace Civil3D.Tools.CutFill.Workflow;

/// <summary>
/// Mutable working state shared between the steps of one cut/fill workflow execution. A fresh
/// instance is created with every workflow; it is never reused across runs, so the mutable
/// collections are safe. Steps write materialized DTOs here; the report step composes them.
/// </summary>
public sealed class CutFillWorkflowState
{
    /// <summary>The request driving this execution.</summary>
    public CutFillRequest Request { get; set; } = new();

    /// <summary>The existing ground (reference) surface loaded by the load step.</summary>
    public SurfaceInfo? ExistingSurface { get; set; }

    /// <summary>The proposed (design) surface loaded by the load step.</summary>
    public SurfaceInfo? ProposedSurface { get; set; }

    /// <summary>The snapshot handed to the calculator by the preparation step.</summary>
    public CutFillCalculationData? Data { get; set; }

    /// <summary>The raw calculator output from the calculation step.</summary>
    public CutFillCalculationResult? Calculation { get; set; }

    /// <summary>The analyzer output from the analysis step.</summary>
    public CutFillAnalysisResult? Analysis { get; set; }

    /// <summary>The composed report; produced by the report step.</summary>
    public CutFillReport? Report { get; set; }
}
