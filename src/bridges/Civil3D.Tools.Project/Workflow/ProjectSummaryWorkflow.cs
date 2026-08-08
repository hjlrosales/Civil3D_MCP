using Civil3D.Domain.Commands;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Project.Analysis;
using Civil3D.Tools.Project.Dtos;

namespace Civil3D.Tools.Project.Workflow;

/// <summary>
/// The <c>project_summary_report</c> workflow: collects drawing and domain data through existing
/// domain services, runs the pure <see cref="ProjectAnalyzer"/> and generates the summary
/// report. Read-only end to end. Steps resolve their dependencies from the workflow context; the
/// tool creates a fresh workflow instance per invocation.
/// </summary>
public sealed class ProjectSummaryWorkflow : IWorkflow<ProjectSummaryReport>
{
    /// <inheritdoc />
    public string Name => "project.summary.report";

    /// <inheritdoc />
    public CommandPermission RequiredPermission => CommandPermission.ReadOnly;

    /// <inheritdoc />
    public TimeSpan? Timeout => null; // The dispatcher applies its default timeout.

    /// <inheritdoc />
    public IReadOnlyList<IWorkflowStep> Steps { get; }

    /// <summary>The per-execution shared state written by the steps.</summary>
    internal ProjectSummaryWorkflowState State { get; }

    /// <summary>Creates the workflow with its steps and shared state.</summary>
    /// <param name="options">Analyzer thresholds; defaults apply when omitted.</param>
    public ProjectSummaryWorkflow(ProjectSummaryOptions? options = null)
    {
        ProjectSummaryOptions opts = options ?? ProjectSummaryOptions.Default;
        State = new ProjectSummaryWorkflowState();

        var steps = new List<IWorkflowStep>
        {
            new ValidateInputStep(opts),
            new CollectDrawingInformationStep(State),
            new CollectDomainObjectsStep(State),
            new AnalyzeRelationshipsStep(State, opts),
        };

        // The summary step needs the final step count; +1 accounts for itself.
        steps.Add(new GenerateSummaryStep(State, totalSteps: steps.Count + 1));
        Steps = steps;
    }
}
