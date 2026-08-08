using Civil3D.Domain.Workflows;
using Civil3D.Tools.Corridor.Dtos;

namespace Civil3D.Tools.Corridor.Workflow;

/// <summary>
/// Runs the corridor-analysis workflow. The base class executes the ordered steps (which load,
/// analyze, recommend and compose the report); this handler only returns the report the steps
/// produced.
/// </summary>
public sealed class CorridorAnalysisWorkflowHandler : WorkflowHandlerBase<CorridorAnalysisWorkflow, CorridorAnalysisReport>
{
    /// <inheritdoc />
    protected override Task<CorridorAnalysisReport> ProduceResultAsync(
        CorridorAnalysisWorkflow workflow, IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            workflow.State.Report
            ?? throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "The corridor-analysis workflow did not produce a report."));
    }
}
