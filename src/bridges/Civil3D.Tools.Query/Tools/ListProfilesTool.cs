using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Domain.Profiles.Dtos;
using Civil3D.Domain.Profiles.Services;
using Civil3D.Domain.Query;
using Civil3D.Tools.Abstractions;

namespace Civil3D.Tools.Query.Tools;

/// <summary>
/// Tool <c>list_profiles</c>: returns a paged, filterable and sortable list of the profiles in the
/// active drawing (see <see cref="QueryRequest"/> for filtering/sorting/paging syntax).
/// Read-only. Fails with E_NO_ACTIVE_DOCUMENT when no drawing is open.
/// </summary>
[McpTool(
    "list_profiles",
    "List Profiles",
    "Returns profiles in the active drawing. Accepts filters, sorting, pagination and field " +
    "selection via a QueryRequest. Read-only; fails with E_NO_ACTIVE_DOCUMENT when no drawing is open.",
    Category = ToolCategory.Profiles,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "profiles", "query", "read-only" })]
public sealed class ListProfilesTool : QueryToolBase<QueryRequest, PageResult<ProfileInfo>>
{
    private readonly IProfileService _profiles;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="profiles">The profile domain service.</param>
    public ListProfilesTool(ICivil3DSession session, IProfileService profiles)
        : base(session)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    }

    /// <inheritdoc />
    protected override Task<PageResult<ProfileInfo>> ExecuteToolCoreAsync(
        QueryRequest input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireActiveDrawing(context);
        return Task.FromResult(RunQuery(context, () => _profiles.Query(input)));
    }
}
