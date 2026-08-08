using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Corridor.Dtos;
using Civil3D.Tools.Corridor.Workflow;
using Civil3D.Tools.Workflows;

namespace Civil3D.Tools.Corridor.Tools;

/// <summary>
/// Tool <c>corridor_analysis_report</c>: analyzes one corridor by id, or every corridor when no
/// id is supplied, and produces a structured, read-only summary and health analysis — per-
/// corridor metrics (baselines, corridor surfaces, style ids), aggregate statistics, health
/// issues and recommendations — derived strictly from the domain layer's exposed corridor data.
/// Runs through the workflow framework with progress reporting, cancellation and structured
/// logging. Fails with <c>E_NO_ACTIVE_DOCUMENT</c> when no drawing is open and
/// <c>E_OBJECT_NOT_FOUND</c> when the supplied id does not exist.
/// </summary>
[McpTool(
    "corridor_analysis_report",
    "Corridor Analysis Report",
    "Analyzes one corridor by id, or every corridor when no id is supplied, and produces a " +
    "structured read-only summary and health report: per-corridor metrics (baselines, corridor " +
    "surfaces, style ids), aggregate statistics, health issues and recommendations derived " +
    "strictly from the exposed domain data. Fails with E_NO_ACTIVE_DOCUMENT when no drawing is " +
    "open and E_OBJECT_NOT_FOUND when the supplied id does not exist.",
    Category = ToolCategory.Corridors,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "corridors", "analysis", "health", "read-only" })]
public sealed class CorridorAnalysisReportTool : WorkflowToolBase<CorridorAnalysisRequest, CorridorAnalysisReport, CorridorAnalysisWorkflow, CorridorAnalysisReport>
{
    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="dispatcher">The workflow dispatcher (validation, permission, timeout, progress, events).</param>
    /// <param name="services">The container exposed to workflow steps via the context.</param>
    public CorridorAnalysisReportTool(
        ICivil3DSession session,
        IWorkflowDispatcher dispatcher,
        IServiceProvider services)
        : base(session, dispatcher, services)
    {
    }

    /// <inheritdoc />
    protected override CorridorAnalysisWorkflow CreateWorkflow(
        CorridorAnalysisRequest input, ToolExecutionContext context)
        => new(input);

    /// <inheritdoc />
    protected override CorridorAnalysisReport MapResult(WorkflowResult<CorridorAnalysisReport> result)
        => result.Data;
}
