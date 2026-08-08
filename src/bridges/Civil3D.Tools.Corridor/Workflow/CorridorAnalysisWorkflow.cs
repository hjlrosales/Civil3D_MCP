using Civil3D.Domain.Commands;
using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Corridor.Analysis;
using Civil3D.Tools.Corridor.Dtos;

namespace Civil3D.Tools.Corridor.Workflow;

/// <summary>
/// The <c>corridor_analysis_report</c> workflow: validates the request, loads one corridor (or
/// all corridors) exactly once through the read-only corridor service, analyzes the
/// <see cref="CorridorInfo"/> snapshots with the pure <see cref="CorridorAnalyzer"/> (never the
/// Civil 3D APIs directly), generates recommendations and composes the health report.
/// Read-only end to end. Steps resolve their dependencies from the workflow context; the tool
/// creates a fresh workflow instance per invocation.
/// </summary>
public sealed class CorridorAnalysisWorkflow : IWorkflow<CorridorAnalysisReport>
{
    /// <inheritdoc />
    public string Name => "corridor.analysis.report";

    /// <inheritdoc />
    public CommandPermission RequiredPermission => CommandPermission.ReadOnly;

    /// <inheritdoc />
    public TimeSpan? Timeout => null; // The dispatcher applies its default timeout.

    /// <inheritdoc />
    public IReadOnlyList<IWorkflowStep> Steps { get; }

    /// <summary>The per-execution shared state written by the steps.</summary>
    internal CorridorWorkflowState State { get; }

    /// <summary>Creates the workflow with its steps and shared state.</summary>
    /// <param name="request">The validated corridor-analysis request.</param>
    public CorridorAnalysisWorkflow(CorridorAnalysisRequest request)
    {
        State = new CorridorWorkflowState { Request = request };

        var steps = new List<IWorkflowStep>
        {
            new ValidateInputStep(request),
            new LoadCorridorDataStep(State),
            new AnalyzeCorridorsStep(State),
            new GenerateRecommendationsStep(State),
        };

        // The report step needs the final step count; +1 accounts for itself.
        steps.Add(new GenerateReportStep(State, totalSteps: steps.Count + 1));
        Steps = steps;
    }
}
