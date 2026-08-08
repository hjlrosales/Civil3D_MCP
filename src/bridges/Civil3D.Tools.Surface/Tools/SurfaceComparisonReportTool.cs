using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Surface.Dtos;
using Civil3D.Tools.Surface.Workflow;
using Civil3D.Tools.Workflows;

namespace Civil3D.Tools.Surface.Tools;

/// <summary>
/// Tool <c>surface_comparison_report</c>: compares two Civil 3D surfaces identified by their ids
/// and produces a structured read-only comparison report — per-metric comparisons, differences
/// with severity, optional numeric statistics and optional recommendations. Runs through the
/// workflow framework with progress reporting, cancellation and structured logging. Fails with
/// <c>E_NO_ACTIVE_DOCUMENT</c> when no drawing is open, <c>E_OBJECT_NOT_FOUND</c> when either id
/// does not exist and <c>E_INVALID_PARAMETERS</c> when the ids are missing or identical.
/// </summary>
[McpTool(
    "surface_comparison_report",
    "Surface Comparison Report",
    "Compares two Civil 3D surfaces by id and produces a structured read-only comparison report: " +
    "per-metric comparisons (name, type, point count, minimum/maximum/average elevation), " +
    "differences with severity, optional numeric statistics and optional recommendations. Fails " +
    "with E_NO_ACTIVE_DOCUMENT when no drawing is open, E_OBJECT_NOT_FOUND when either id does " +
    "not exist and E_INVALID_PARAMETERS when the ids are missing or identical.",
    Category = ToolCategory.Surfaces,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "surfaces", "comparison", "report", "read-only" })]
public sealed class SurfaceComparisonReportTool : WorkflowToolBase<SurfaceComparisonRequest, SurfaceComparisonReport, SurfaceComparisonWorkflow, SurfaceComparisonReport>
{
    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="dispatcher">The workflow dispatcher (validation, permission, timeout, progress, events).</param>
    /// <param name="services">The container exposed to workflow steps via the context.</param>
    public SurfaceComparisonReportTool(
        ICivil3DSession session,
        IWorkflowDispatcher dispatcher,
        IServiceProvider services)
        : base(session, dispatcher, services)
    {
    }

    /// <inheritdoc />
    protected override SurfaceComparisonWorkflow CreateWorkflow(
        SurfaceComparisonRequest input, ToolExecutionContext context)
        => new(input);

    /// <inheritdoc />
    protected override SurfaceComparisonReport MapResult(WorkflowResult<SurfaceComparisonReport> result)
        => result.Data;
}
