using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.CutFill.Dtos;
using Civil3D.Tools.CutFill.Workflow;
using Civil3D.Tools.Workflows;

namespace Civil3D.Tools.CutFill.Tools;

/// <summary>
/// Tool <c>calculate_cut_fill</c>: compares an existing ground surface with a proposed surface
/// and produces a structured, read-only earthwork volume report — cut/fill/net volumes (or a
/// structured not-supported result when the installed API exposes no reliable read-only volume
/// path), surface differences, optional statistics and optional recommendations. Runs through
/// the workflow framework with progress reporting, cancellation and structured logging. Fails
/// with <c>E_NO_ACTIVE_DOCUMENT</c> when no drawing is open, <c>E_OBJECT_NOT_FOUND</c> when
/// either id does not exist and <c>E_INVALID_PARAMETERS</c> when the ids are missing or
/// identical.
/// </summary>
[McpTool(
    "calculate_cut_fill",
    "Calculate Cut & Fill",
    "Compares an existing ground surface with a proposed surface and produces a structured " +
    "read-only earthwork volume report: cut/fill/net volumes (or a structured not-supported " +
    "result when the installed API exposes no reliable read-only volume path), surface " +
    "differences, optional statistics and optional recommendations. Fails with " +
    "E_NO_ACTIVE_DOCUMENT when no drawing is open, E_OBJECT_NOT_FOUND when either id does not " +
    "exist and E_INVALID_PARAMETERS when the ids are missing or identical.",
    Category = ToolCategory.Surfaces,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "surfaces", "cut-fill", "earthwork", "volumes", "read-only" })]
public sealed class CalculateCutFillTool : WorkflowToolBase<CutFillRequest, CutFillReport, CutFillWorkflow, CutFillReport>
{
    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="dispatcher">The workflow dispatcher (validation, permission, timeout, progress, events).</param>
    /// <param name="services">The container exposed to workflow steps via the context.</param>
    public CalculateCutFillTool(
        ICivil3DSession session,
        IWorkflowDispatcher dispatcher,
        IServiceProvider services)
        : base(session, dispatcher, services)
    {
    }

    /// <inheritdoc />
    protected override CutFillWorkflow CreateWorkflow(
        CutFillRequest input, ToolExecutionContext context)
        => new(input);

    /// <inheritdoc />
    protected override CutFillReport MapResult(WorkflowResult<CutFillReport> result)
        => result.Data;
}
