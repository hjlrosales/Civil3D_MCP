using Civil3D.Domain.Workflows;
using Civil3D.Tools.Quantity.Dtos;

namespace Civil3D.Tools.Quantity.Workflow;

/// <summary>
/// Runs the quantity-takeoff workflow. The base class executes the ordered steps (which collect,
/// calculate and compose the report); this handler only returns the report the steps produced.
/// </summary>
public sealed class QuantityTakeoffWorkflowHandler : WorkflowHandlerBase<QuantityTakeoffWorkflow, QuantityTakeoffReport>
{
    /// <inheritdoc />
    protected override Task<QuantityTakeoffReport> ProduceResultAsync(
        QuantityTakeoffWorkflow workflow, IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            workflow.State.Report
            ?? throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "The quantity takeoff workflow did not produce a report."));
    }
}
