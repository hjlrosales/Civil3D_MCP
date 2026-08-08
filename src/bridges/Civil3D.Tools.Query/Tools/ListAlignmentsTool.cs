using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Alignments.Services;
using Civil3D.Domain.Query;
using Civil3D.Tools.Abstractions;

namespace Civil3D.Tools.Query.Tools;

/// <summary>
/// Tool <c>list_alignments</c>: returns a paged, filterable and sortable list of the alignments in
/// the active drawing (see <see cref="QueryRequest"/> for filtering/sorting/paging syntax).
/// Read-only. Fails with E_NO_ACTIVE_DOCUMENT when no drawing is open.
/// </summary>
[McpTool(
    "list_alignments",
    "List Alignments",
    "Returns alignments in the active drawing. Accepts filters, sorting, pagination and field " +
    "selection via a QueryRequest. Read-only; fails with E_NO_ACTIVE_DOCUMENT when no drawing is open.",
    Category = ToolCategory.Alignments,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "alignments", "query", "read-only" })]
public sealed class ListAlignmentsTool : QueryToolBase<QueryRequest, PageResult<AlignmentInfo>>
{
    private readonly IAlignmentService _alignments;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="alignments">The alignment domain service.</param>
    public ListAlignmentsTool(ICivil3DSession session, IAlignmentService alignments)
        : base(session)
    {
        _alignments = alignments ?? throw new ArgumentNullException(nameof(alignments));
    }

    /// <inheritdoc />
    protected override Task<PageResult<AlignmentInfo>> ExecuteToolCoreAsync(
        QueryRequest input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireActiveDrawing(context);
        return Task.FromResult(RunQuery(context, () => _alignments.Query(input)));
    }
}
