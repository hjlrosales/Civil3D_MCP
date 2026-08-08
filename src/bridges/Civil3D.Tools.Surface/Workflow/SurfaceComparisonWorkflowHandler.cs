using Civil3D.Domain.Workflows;
using Civil3D.Tools.Surface.Dtos;

namespace Civil3D.Tools.Surface.Workflow;

/// <summary>
/// Runs the surface-comparison workflow. The base class executes the ordered steps (which load,
/// compare and compose the report); this handler only returns the report the steps produced.
/// </summary>
public sealed class SurfaceComparisonWorkflowHandler : WorkflowHandlerBase<SurfaceComparisonWorkflow, SurfaceComparisonReport>
{
    /// <inheritdoc />
    protected override Task<SurfaceComparisonReport> ProduceResultAsync(
        SurfaceComparisonWorkflow workflow, IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            workflow.State.Report
            ?? throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "The surface comparison workflow did not produce a report."));
    }
}
