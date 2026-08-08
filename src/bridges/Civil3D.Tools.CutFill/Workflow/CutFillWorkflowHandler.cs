using Civil3D.Domain.Workflows;
using Civil3D.Tools.CutFill.Dtos;

namespace Civil3D.Tools.CutFill.Workflow;

/// <summary>
/// Runs the cut/fill workflow. The base class executes the ordered steps (which load, calculate,
/// analyze and compose the report); this handler only returns the report the steps produced.
/// </summary>
public sealed class CutFillWorkflowHandler : WorkflowHandlerBase<CutFillWorkflow, CutFillReport>
{
    /// <inheritdoc />
    protected override Task<CutFillReport> ProduceResultAsync(
        CutFillWorkflow workflow, IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            workflow.State.Report
            ?? throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "The cut/fill workflow did not produce a report."));
    }
}
