using Civil3D.Domain.Commands;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Surface.Analysis;
using Civil3D.Tools.Surface.Dtos;

namespace Civil3D.Tools.Surface.Workflow;

/// <summary>
/// The <c>surface_comparison_report</c> workflow: validates the request, loads both surfaces
/// exactly once through the read-only surface service, runs the pure <see cref="SurfaceComparer"/>
/// and generates the comparison report. Read-only end to end. Steps resolve their dependencies
/// from the workflow context; the tool creates a fresh workflow instance per invocation.
/// </summary>
public sealed class SurfaceComparisonWorkflow : IWorkflow<SurfaceComparisonReport>
{
    /// <inheritdoc />
    public string Name => "surface.comparison.report";

    /// <inheritdoc />
    public CommandPermission RequiredPermission => CommandPermission.ReadOnly;

    /// <inheritdoc />
    public TimeSpan? Timeout => null; // The dispatcher applies its default timeout.

    /// <inheritdoc />
    public IReadOnlyList<IWorkflowStep> Steps { get; }

    /// <summary>The per-execution shared state written by the steps.</summary>
    internal SurfaceComparisonWorkflowState State { get; }

    /// <summary>Creates the workflow with its steps and shared state.</summary>
    /// <param name="request">The validated comparison request.</param>
    public SurfaceComparisonWorkflow(SurfaceComparisonRequest request)
    {
        State = new SurfaceComparisonWorkflowState { Request = request };

        var steps = new List<IWorkflowStep>
        {
            new ValidateInputStep(request),
            new LoadSurfaceMetadataStep(State),
            new LoadComparisonDataStep(State),
            new AnalyzeDifferencesStep(State),
        };

        // The report step needs the final step count; +1 accounts for itself.
        steps.Add(new GenerateReportStep(State, totalSteps: steps.Count + 1));
        Steps = steps;
    }
}
