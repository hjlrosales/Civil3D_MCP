using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Quantity.Dtos;
using Civil3D.Tools.Quantity.Workflow;
using Civil3D.Tools.Workflows;

namespace Civil3D.Tools.Quantity.Tools;

/// <summary>
/// Tool <c>quantity_takeoff_report</c>: produces a structured, read-only quantity summary of the
/// active Civil 3D drawing — object inventories by discipline, per-item quantity lines,
/// per-category roll-ups and aggregate statistics (alignment/profile lengths, surface points,
/// corridor baselines/surfaces, pipes and structures, COGO points, style usage and drawing-level
/// counts). Runs through the workflow framework with progress reporting, cancellation and
/// structured logging. Fails with <c>E_NO_ACTIVE_DOCUMENT</c> when no drawing is open.
/// </summary>
[McpTool(
    "quantity_takeoff_report",
    "Quantity Takeoff Report",
    "Produces a structured read-only quantity summary of the active Civil 3D drawing: object " +
    "inventories by discipline, quantity line items, per-category roll-ups and aggregate " +
    "statistics (alignment and profile lengths, surface points, corridor baselines and surfaces, " +
    "pipes and structures, COGO points, style usage and drawing-level counts). Fails with " +
    "E_NO_ACTIVE_DOCUMENT when no drawing is open.",
    Category = ToolCategory.Drawing,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "drawing", "quantity", "takeoff", "inventory", "read-only" })]
public sealed class QuantityTakeoffReportTool : WorkflowToolBase<EmptyParameters, QuantityTakeoffReport, QuantityTakeoffWorkflow, QuantityTakeoffReport>
{
    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="dispatcher">The workflow dispatcher (validation, permission, timeout, progress, events).</param>
    /// <param name="services">The container exposed to workflow steps via the context.</param>
    public QuantityTakeoffReportTool(
        ICivil3DSession session,
        IWorkflowDispatcher dispatcher,
        IServiceProvider services)
        : base(session, dispatcher, services)
    {
    }

    /// <inheritdoc />
    protected override QuantityTakeoffWorkflow CreateWorkflow(EmptyParameters input, ToolExecutionContext context)
        => new();

    /// <inheritdoc />
    protected override QuantityTakeoffReport MapResult(WorkflowResult<QuantityTakeoffReport> result)
        => result.Data;
}
