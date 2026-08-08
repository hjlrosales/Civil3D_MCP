using Civil3D.Domain.Workflows;
using Civil3D.Tools.Validation.Dtos;

namespace Civil3D.Tools.Validation.Workflow;

/// <summary>
/// Runs the design-validation workflow. The base class executes the ordered steps (which collect,
/// validate, aggregate and compose the report); this handler only returns the report the steps
/// produced.
/// </summary>
public sealed class DesignValidationWorkflowHandler : WorkflowHandlerBase<DesignValidationWorkflow, DesignValidationReport>
{
    /// <inheritdoc />
    protected override Task<DesignValidationReport> ProduceResultAsync(
        DesignValidationWorkflow workflow, IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            workflow.State.Report
            ?? throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "The design validation workflow did not produce a report."));
    }
}
