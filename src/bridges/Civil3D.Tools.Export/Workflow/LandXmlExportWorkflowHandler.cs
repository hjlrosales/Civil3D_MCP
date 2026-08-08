using Civil3D.Domain.Workflows;
using Civil3D.Tools.Export.Dtos;

namespace Civil3D.Tools.Export.Workflow;

/// <summary>
/// Runs the LandXML export workflow. The base class executes the ordered steps (which validate,
/// collect, build, export, validate the output and compose the report); this handler only
/// returns the report the steps produced.
/// </summary>
public sealed class LandXmlExportWorkflowHandler : WorkflowHandlerBase<LandXmlExportWorkflow, LandXmlExportReport>
{
    /// <inheritdoc />
    protected override Task<LandXmlExportReport> ProduceResultAsync(
        LandXmlExportWorkflow workflow, IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            workflow.State.Report
            ?? throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "The LandXML export workflow did not produce a report."));
    }
}
