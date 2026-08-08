using Civil3D.Domain.Workflows;
using Civil3D.Tools.Project.Dtos;

namespace Civil3D.Tools.Project.Workflow;

/// <summary>
/// Runs the project-summary workflow. The base class executes the ordered steps (which collect,
/// analyze and compose the report); this handler only returns the report the steps produced.
/// </summary>
public sealed class ProjectSummaryWorkflowHandler : WorkflowHandlerBase<ProjectSummaryWorkflow, ProjectSummaryReport>
{
    /// <inheritdoc />
    protected override Task<ProjectSummaryReport> ProduceResultAsync(
        ProjectSummaryWorkflow workflow, IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            workflow.State.Report
            ?? throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "The project summary workflow did not produce a report."));
    }
}
