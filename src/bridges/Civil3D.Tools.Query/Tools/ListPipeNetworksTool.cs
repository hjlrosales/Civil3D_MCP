using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Pipes.Services;
using Civil3D.Domain.Query;
using Civil3D.Tools.Abstractions;

namespace Civil3D.Tools.Query.Tools;

/// <summary>
/// Tool <c>list_pipe_networks</c>: returns a paged, filterable and sortable list of the pipe
/// networks in the active drawing (see <see cref="QueryRequest"/> for filtering/sorting/paging
/// syntax). Read-only. Fails with E_NO_ACTIVE_DOCUMENT when no drawing is open.
/// </summary>
[McpTool(
    "list_pipe_networks",
    "List Pipe Networks",
    "Returns pipe networks in the active drawing. Accepts filters, sorting, pagination and field " +
    "selection via a QueryRequest. Read-only; fails with E_NO_ACTIVE_DOCUMENT when no drawing is open.",
    Category = ToolCategory.PipeNetworks,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "pipe-networks", "query", "read-only" })]
public sealed class ListPipeNetworksTool : QueryToolBase<QueryRequest, PageResult<PipeNetworkInfo>>
{
    private readonly IPipeService _pipes;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="pipes">The pipe network domain service.</param>
    public ListPipeNetworksTool(ICivil3DSession session, IPipeService pipes)
        : base(session)
    {
        _pipes = pipes ?? throw new ArgumentNullException(nameof(pipes));
    }

    /// <inheritdoc />
    protected override Task<PageResult<PipeNetworkInfo>> ExecuteToolCoreAsync(
        QueryRequest input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireActiveDrawing(context);
        return Task.FromResult(RunQuery(context, () => _pipes.Query(input)));
    }
}
