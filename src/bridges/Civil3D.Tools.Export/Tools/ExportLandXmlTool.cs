using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Export.Dtos;
using Civil3D.Tools.Export.Workflow;
using Civil3D.Tools.Workflows;

namespace Civil3D.Tools.Export.Tools;

/// <summary>
/// Tool <c>export_landxml</c>: exports the included Civil 3D object types into a LandXML file
/// and produces a structured, read-only export report — the written file's location and size
/// (or a structured not-supported result when the installed API exposes no reliable export
/// path), per-type statistics, the exported and skipped object lists and recommendations. The
/// drawing is never modified. Runs through the workflow framework with progress reporting,
/// cancellation and structured logging. Fails with <c>E_NO_ACTIVE_DOCUMENT</c> when no drawing
/// is open and <c>E_INVALID_PARAMETERS</c> when the inputs are invalid.
/// </summary>
[McpTool(
    "export_landxml",
    "Export LandXML",
    "Exports the included Civil 3D object types into a LandXML file and produces a structured " +
    "read-only export report: the written file's location and size (or a structured " +
    "not-supported result when the installed API exposes no reliable export path), per-type " +
    "statistics, exported and skipped objects and recommendations. The drawing is never " +
    "modified. Fails with E_NO_ACTIVE_DOCUMENT when no drawing is open and E_INVALID_PARAMETERS " +
    "when the inputs are invalid.",
    Category = ToolCategory.Export,
    Permission = ToolPermission.Export,
    Risk = ToolRisk.Medium,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "export", "landxml", "interoperability", "read-only" })]
public sealed class ExportLandXmlTool : WorkflowToolBase<LandXmlExportRequest, LandXmlExportReport, LandXmlExportWorkflow, LandXmlExportReport>
{
    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="dispatcher">The workflow dispatcher (validation, permission, timeout, progress, events).</param>
    /// <param name="services">The container exposed to workflow steps via the context.</param>
    public ExportLandXmlTool(
        ICivil3DSession session,
        IWorkflowDispatcher dispatcher,
        IServiceProvider services)
        : base(session, dispatcher, services)
    {
    }

    /// <inheritdoc />
    protected override LandXmlExportWorkflow CreateWorkflow(
        LandXmlExportRequest input, ToolExecutionContext context)
        => new(input);

    /// <inheritdoc />
    protected override LandXmlExportReport MapResult(WorkflowResult<LandXmlExportReport> result)
        => result.Data;
}
