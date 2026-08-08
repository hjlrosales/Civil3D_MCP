using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Tools.Corridor.Analysis;
using Civil3D.Tools.Corridor.Dtos;

namespace Civil3D.Tools.Corridor.Workflow;

/// <summary>
/// Mutable working state shared between the steps of one corridor-analysis workflow execution.
/// A fresh instance is created with every workflow; it is never reused across runs, so the
/// mutable collection is safe. Steps write materialized DTOs here; the report step composes them.
/// </summary>
public sealed class CorridorWorkflowState
{
    /// <summary>The request driving this execution.</summary>
    public CorridorAnalysisRequest Request { get; set; } = new();

    /// <summary>The corridors to analyze, loaded exactly once by the load step.</summary>
    public IReadOnlyList<CorridorInfo> Corridors { get; set; } = Array.Empty<CorridorInfo>();

    /// <summary>The analyzer output from the analysis and recommendation steps.</summary>
    public CorridorAnalysisResult? Analysis { get; set; }

    /// <summary>The composed report; produced by the report step.</summary>
    public CorridorAnalysisReport? Report { get; set; }
}
