using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Project.Dtos;
using Civil3D.Tools.Project.Workflow;
using Civil3D.Tools.Workflows;

namespace Civil3D.Tools.Project.Tools;

/// <summary>
/// Tool <c>project_summary_report</c>: produces a comprehensive read-only overview of the active
/// Civil 3D drawing — drawing metadata, object inventory, reference integrity, complexity
/// classification, statistics and recommendations — intended to give AI clients immediate
/// context about the project. Runs through the workflow framework with progress reporting,
/// cancellation and structured logging. Read-only; fails with <c>E_NO_ACTIVE_DOCUMENT</c> when no
/// drawing is open.
/// </summary>
[McpTool(
    "project_summary_report",
    "Project Summary Report",
    "Produces a comprehensive read-only overview of the active Civil 3D drawing: drawing metadata, " +
    "object inventory (alignments, profiles, surfaces, corridors, pipe networks, COGO points, " +
    "styles, layers, blocks, xrefs, viewports, text/dimension styles, linetypes), reference " +
    "integrity, a complexity classification and recommended next steps. Fails with " +
    "E_NO_ACTIVE_DOCUMENT when no drawing is open.",
    Category = ToolCategory.Drawing,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "drawing", "project", "summary", "overview", "read-only" })]
public sealed class ProjectSummaryReportTool : WorkflowToolBase<EmptyParameters, ProjectSummaryReport, ProjectSummaryWorkflow, ProjectSummaryReport>
{
    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="dispatcher">The workflow dispatcher (validation, permission, timeout, progress, events).</param>
    /// <param name="services">The container exposed to workflow steps via the context.</param>
    public ProjectSummaryReportTool(
        ICivil3DSession session,
        IWorkflowDispatcher dispatcher,
        IServiceProvider services)
        : base(session, dispatcher, services)
    {
    }

    /// <inheritdoc />
    protected override ProjectSummaryWorkflow CreateWorkflow(EmptyParameters input, ToolExecutionContext context)
        => new();

    /// <inheritdoc />
    protected override ProjectSummaryReport MapResult(WorkflowResult<ProjectSummaryReport> result)
        => result.Data;
}
