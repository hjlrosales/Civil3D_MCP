using Civil3D.Domain.Commands;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Health.Analysis;
using Civil3D.Tools.Health.Dtos;

namespace Civil3D.Tools.Health.Workflow;

/// <summary>
/// The <c>drawing_health_report</c> workflow: collects drawing and domain data through existing
/// domain services, runs the pure <see cref="HealthAnalyzer"/> and generates the report. Read-only
/// end to end. Steps resolve their dependencies from the workflow context; the tool creates a fresh
/// workflow instance per invocation.
/// </summary>
public sealed class DrawingHealthWorkflow : IWorkflow<DrawingHealthReport>
{
    /// <inheritdoc />
    public string Name => "drawing.health.report";

    /// <inheritdoc />
    public CommandPermission RequiredPermission => CommandPermission.ReadOnly;

    /// <inheritdoc />
    public TimeSpan? Timeout => null; // The dispatcher applies its default timeout.

    /// <inheritdoc />
    public IReadOnlyList<IWorkflowStep> Steps { get; }

    /// <summary>The per-execution shared state written by the steps.</summary>
    internal DrawingHealthWorkflowState State { get; }

    /// <summary>Creates the workflow with its steps and shared state.</summary>
    /// <param name="options">Analyzer thresholds; defaults apply when omitted.</param>
    public DrawingHealthWorkflow(HealthAnalyzerOptions? options = null)
    {
        HealthAnalyzerOptions opts = options ?? HealthAnalyzerOptions.Default;
        State = new DrawingHealthWorkflowState();

        var steps = new List<IWorkflowStep>
        {
            new ValidateInputStep(opts),
            new CollectDrawingInformationStep(State),
            new CollectDomainDataStep(State),
            new AnalyzeResultsStep(State, opts),
        };

        // The report step needs the final step count; +1 accounts for itself.
        steps.Add(new GenerateReportStep(State, totalSteps: steps.Count + 1));
        Steps = steps;
    }
}
