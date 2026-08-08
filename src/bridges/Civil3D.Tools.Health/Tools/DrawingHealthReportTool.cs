using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Health.Dtos;
using Civil3D.Tools.Health.Workflow;
using Civil3D.Tools.Workflows;

namespace Civil3D.Tools.Health.Tools;

/// <summary>
/// Tool <c>drawing_health_report</c>: produces a read-only health summary of the active drawing
/// (drawing state, drawing statistics, domain collections, analysis findings with recommendations
/// and execution summary). Runs through the workflow framework with progress reporting,
/// cancellation and structured logging. Read-only; fails with <c>E_NO_ACTIVE_DOCUMENT</c> when no
/// drawing is open.
/// </summary>
[McpTool(
    "drawing_health_report",
    "Drawing Health Report",
    "Produces a comprehensive, read-only health summary of the active drawing: drawing state, " +
    "drawing statistics, alignment/surface/profile/corridor/pipe/COGO/style collections, and " +
    "analysis findings with recommendations (empty collections, duplicate names, missing " +
    "descriptions, orphaned references, missing and unused styles, large collections, locked " +
    "points, drawing state). Fails with E_NO_ACTIVE_DOCUMENT when no drawing is open.",
    Category = ToolCategory.Drawing,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "drawing", "health", "report", "analysis", "read-only" })]
public sealed class DrawingHealthReportTool : WorkflowToolBase<EmptyParameters, DrawingHealthReport, DrawingHealthWorkflow, DrawingHealthReport>
{
    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="dispatcher">The workflow dispatcher (validation, permission, timeout, progress, events).</param>
    /// <param name="services">The container exposed to workflow steps via the context.</param>
    public DrawingHealthReportTool(
        ICivil3DSession session,
        IWorkflowDispatcher dispatcher,
        IServiceProvider services)
        : base(session, dispatcher, services)
    {
    }

    /// <inheritdoc />
    protected override DrawingHealthWorkflow CreateWorkflow(EmptyParameters input, ToolExecutionContext context)
        => new();

    /// <inheritdoc />
    protected override DrawingHealthReport MapResult(WorkflowResult<DrawingHealthReport> result)
        => result.Data;
}
