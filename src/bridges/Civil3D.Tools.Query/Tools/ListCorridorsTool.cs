using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Domain.Corridors.Services;
using Civil3D.Domain.Query;
using Civil3D.Tools.Abstractions;

namespace Civil3D.Tools.Query.Tools;

/// <summary>
/// Tool <c>list_corridors</c>: returns a paged, filterable and sortable list of the corridors in
/// the active drawing (see <see cref="QueryRequest"/> for filtering/sorting/paging syntax).
/// Read-only. Fails with E_NO_ACTIVE_DOCUMENT when no drawing is open.
/// </summary>
[McpTool(
    "list_corridors",
    "List Corridors",
    "Returns corridors in the active drawing. Accepts filters, sorting, pagination and field " +
    "selection via a QueryRequest. Read-only; fails with E_NO_ACTIVE_DOCUMENT when no drawing is open.",
    Category = ToolCategory.Corridors,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "corridors", "query", "read-only" })]
public sealed class ListCorridorsTool : QueryToolBase<QueryRequest, PageResult<CorridorInfo>>
{
    private readonly ICorridorService _corridors;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="corridors">The corridor domain service.</param>
    public ListCorridorsTool(ICivil3DSession session, ICorridorService corridors)
        : base(session)
    {
        _corridors = corridors ?? throw new ArgumentNullException(nameof(corridors));
    }

    /// <inheritdoc />
    protected override Task<PageResult<CorridorInfo>> ExecuteToolCoreAsync(
        QueryRequest input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireActiveDrawing(context);
        return Task.FromResult(RunQuery(context, () => _corridors.Query(input)))
;
    }
}
