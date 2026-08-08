using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Domain.Cogo.Dtos;
using Civil3D.Domain.Cogo.Services;
using Civil3D.Domain.Query;
using Civil3D.Tools.Abstractions;

namespace Civil3D.Tools.Query.Tools;

/// <summary>
/// Tool <c>list_cogo_points</c>: returns a paged, filterable and sortable list of the COGO points
/// in the active drawing (see <see cref="QueryRequest"/> for filtering/sorting/paging syntax).
/// Read-only. Fails with E_NO_ACTIVE_DOCUMENT when no drawing is open.
/// </summary>
[McpTool(
    "list_cogo_points",
    "List COGO Points",
    "Returns COGO points in the active drawing. Accepts filters, sorting, pagination and field " +
    "selection via a QueryRequest. Read-only; fails with E_NO_ACTIVE_DOCUMENT when no drawing is open.",
    Category = ToolCategory.Cogo,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "cogo", "points", "query", "read-only" })]
public sealed class ListCogoPointsTool : QueryToolBase<QueryRequest, PageResult<CogoPointInfo>>
{
    private readonly ICogoService _cogo;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="cogo">The COGO domain service.</param>
    public ListCogoPointsTool(ICivil3DSession session, ICogoService cogo)
        : base(session)
    {
        _cogo = cogo ?? throw new ArgumentNullException(nameof(cogo));
    }

    /// <inheritdoc />
    protected override Task<PageResult<CogoPointInfo>> ExecuteToolCoreAsync(
        QueryRequest input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireActiveDrawing(context);
        return Task.FromResult(RunQuery(context, () => _cogo.Query(input)));
    }
}
