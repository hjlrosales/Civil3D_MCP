using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Domain.Query;
using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Domain.Surfaces.Services;
using Civil3D.Tools.Abstractions;

namespace Civil3D.Tools.Query.Tools;

/// <summary>
/// Tool <c>list_surfaces</c>: returns a paged, filterable and sortable list of the surfaces in the
/// active drawing (see <see cref="QueryRequest"/> for filtering/sorting/paging syntax).
/// Read-only. Fails with E_NO_ACTIVE_DOCUMENT when no drawing is open.
/// </summary>
[McpTool(
    "list_surfaces",
    "List Surfaces",
    "Returns surfaces in the active drawing. Accepts filters, sorting, pagination and field " +
    "selection via a QueryRequest. Read-only; fails with E_NO_ACTIVE_DOCUMENT when no drawing is open.",
    Category = ToolCategory.Surfaces,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "surfaces", "query", "read-only" })]
public sealed class ListSurfacesTool : QueryToolBase<QueryRequest, PageResult<SurfaceInfo>>
{
    private readonly ISurfaceService _surfaces;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="surfaces">The surface domain service.</param>
    public ListSurfacesTool(ICivil3DSession session, ISurfaceService surfaces)
        : base(session)
    {
        _surfaces = surfaces ?? throw new ArgumentNullException(nameof(surfaces));
    }

    /// <inheritdoc />
    protected override Task<PageResult<SurfaceInfo>> ExecuteToolCoreAsync(
        QueryRequest input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireActiveDrawing(context);
        return Task.FromResult(RunQuery(context, () => _surfaces.Query(input)));
    }
}
