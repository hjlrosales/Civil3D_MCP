using Civil3D.Domain.Workflows;
using Civil3D.Tools.Health.Dtos;

namespace Civil3D.Tools.Health.Workflow;

/// <summary>
/// Runs the drawing-health workflow. The base class executes the ordered steps (which collect,
/// analyze and compose the report); this handler only returns the report the steps produced.
/// </summary>
public sealed class DrawingHealthWorkflowHandler : WorkflowHandlerBase<DrawingHealthWorkflow, DrawingHealthReport>
{
    /// <inheritdoc />
    protected override Task<DrawingHealthReport> ProduceResultAsync(
        DrawingHealthWorkflow workflow, IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            workflow.State.Report
            ?? throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "The drawing health workflow did not produce a report."));
    }
}
