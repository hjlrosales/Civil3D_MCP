using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Validation.Dtos;
using Civil3D.Tools.Validation.Workflow;
using Civil3D.Tools.Workflows;

namespace Civil3D.Tools.Validation.Tools;

/// <summary>
/// Tool <c>design_validation_report</c>: runs every registered validation rule against the active
/// Civil 3D drawing and returns a consolidated read-only report of the findings, severity
/// roll-ups and recommendations. Runs through the workflow framework with progress reporting,
/// cancellation and structured logging. Fails with <c>E_NO_ACTIVE_DOCUMENT</c> when no drawing is
/// open.
/// </summary>
[McpTool(
    "design_validation_report",
    "Design Validation Report",
    "Runs the registered design validation rules against the active Civil 3D drawing and returns " +
    "a consolidated read-only report: duplicate names, missing descriptions, unexpectedly empty " +
    "collections, unresolved references, unused styles, duplicate COGO point numbers, profiles " +
    "without alignments and pipe networks without structures, plus severity roll-ups and " +
    "recommendations. Fails with E_NO_ACTIVE_DOCUMENT when no drawing is open.",
    Category = ToolCategory.Drawing,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "drawing", "validation", "design", "report", "quality", "read-only" })]
public sealed class DesignValidationReportTool : WorkflowToolBase<EmptyParameters, DesignValidationReport, DesignValidationWorkflow, DesignValidationReport>
{
    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="dispatcher">The workflow dispatcher (validation, permission, timeout, progress, events).</param>
    /// <param name="services">The container exposed to workflow steps via the context.</param>
    public DesignValidationReportTool(
        ICivil3DSession session,
        IWorkflowDispatcher dispatcher,
        IServiceProvider services)
        : base(session, dispatcher, services)
    {
    }

    /// <inheritdoc />
    protected override DesignValidationWorkflow CreateWorkflow(EmptyParameters input, ToolExecutionContext context)
        => new();

    /// <inheritdoc />
    protected override DesignValidationReport MapResult(WorkflowResult<DesignValidationReport> result)
        => result.Data;
}
