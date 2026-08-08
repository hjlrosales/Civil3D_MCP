using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Domain.Query;
using Civil3D.Domain.Styles.Dtos;
using Civil3D.Domain.Styles.Services;
using Civil3D.Tools.Abstractions;

namespace Civil3D.Tools.Query.Tools;

/// <summary>
/// Tool <c>list_styles</c>: returns a paged, filterable and sortable list of the Civil 3D styles
/// in the active drawing (see <see cref="QueryRequest"/> for filtering/sorting/paging syntax).
/// Read-only. Fails with E_NO_ACTIVE_DOCUMENT when no drawing is open.
/// </summary>
[McpTool(
    "list_styles",
    "List Styles",
    "Returns Civil 3D styles in the active drawing. Accepts filters, sorting, pagination and " +
    "field selection via a QueryRequest. Read-only; fails with E_NO_ACTIVE_DOCUMENT when no drawing is open.",
    Category = ToolCategory.Styles,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "styles", "query", "read-only" })]
public sealed class ListStylesTool : QueryToolBase<QueryRequest, PageResult<StyleInfo>>
{
    private readonly IStyleService _styles;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="styles">The style domain service.</param>
    public ListStylesTool(ICivil3DSession session, IStyleService styles)
        : base(session)
    {
        _styles = styles ?? throw new ArgumentNullException(nameof(styles));
    }

    /// <inheritdoc />
    protected override Task<PageResult<StyleInfo>> ExecuteToolCoreAsync(
        QueryRequest input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireActiveDrawing(context);
        return Task.FromResult(RunQuery(context, () => _styles.Query(input)));
    }
}
