using Civil3D.Domain.Commands;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.CutFill.Abstractions;
using Civil3D.Tools.CutFill.Analysis;
using Civil3D.Tools.CutFill.Dtos;

namespace Civil3D.Tools.CutFill.Workflow;

/// <summary>
/// The <c>calculate_cut_fill</c> workflow: validates the request, loads both surfaces exactly
/// once through the read-only surface service, runs the <see cref="ICutFillCalculator"/> (never
/// the Civil 3D APIs directly), analyzes the output with the pure <see cref="CutFillAnalyzer"/>
/// and generates the earthwork volume report. Read-only end to end. Steps resolve their
/// dependencies from the workflow context; the tool creates a fresh workflow instance per
/// invocation.
/// </summary>
public sealed class CutFillWorkflow : IWorkflow<CutFillReport>
{
    /// <inheritdoc />
    public string Name => "calculate.cut.fill";

    /// <inheritdoc />
    public CommandPermission RequiredPermission => CommandPermission.ReadOnly;

    /// <inheritdoc />
    public TimeSpan? Timeout => null; // The dispatcher applies its default timeout.

    /// <inheritdoc />
    public IReadOnlyList<IWorkflowStep> Steps { get; }

    /// <summary>The per-execution shared state written by the steps.</summary>
    internal CutFillWorkflowState State { get; }

    /// <summary>Creates the workflow with its steps and shared state.</summary>
    /// <param name="request">The validated cut/fill request.</param>
    public CutFillWorkflow(CutFillRequest request)
    {
        State = new CutFillWorkflowState { Request = request };

        var steps = new List<IWorkflowStep>
        {
            new ValidateInputStep(request),
            new LoadSurfacesStep(State),
            new PrepareCalculationStep(State),
            new ExecuteCalculationStep(State),
            new AnalyzeResultsStep(State),
        };

        // The report step needs the final step count; +1 accounts for itself.
        steps.Add(new GenerateReportStep(State, totalSteps: steps.Count + 1));
        Steps = steps;
    }
}
