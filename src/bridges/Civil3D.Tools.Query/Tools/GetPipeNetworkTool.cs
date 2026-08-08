using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Autodesk.Mcp.Shared.Errors;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Pipes.Services;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Query.Dtos;

namespace Civil3D.Tools.Query.Tools;

/// <summary>
/// Tool <c>get_pipe_network</c>: returns a single pipe network by its stable numeric id.
/// Read-only. Fails with E_NO_ACTIVE_DOCUMENT when no drawing is open and E_OBJECT_NOT_FOUND
/// when the id does not exist.
/// </summary>
[McpTool(
    "get_pipe_network",
    "Get Pipe Network",
    "Returns a single pipe network by id. Read-only; fails with E_NO_ACTIVE_DOCUMENT when no " +
    "drawing is open and E_OBJECT_NOT_FOUND when the id does not exist.",
    Category = ToolCategory.PipeNetworks,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "pipe-networks", "lookup", "read-only" })]
public sealed class GetPipeNetworkTool : QueryToolBase<IdRequest, PipeNetworkInfo>
{
    private readonly IPipeService _pipes;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="pipes">The pipe network domain service.</param>
    public GetPipeNetworkTool(ICivil3DSession session, IPipeService pipes)
        : base(session)
    {
        _pipes = pipes ?? throw new ArgumentNullException(nameof(pipes));
    }

    /// <inheritdoc />
    protected override Task<PipeNetworkInfo> ExecuteToolCoreAsync(
        IdRequest input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireActiveDrawing(context);

        PipeNetworkInfo? result = RunQuery(context, () => _pipes.GetById(input.Id));
        return Task.FromResult(result ?? throw new BridgeException(
            ErrorCode.E_OBJECT_NOT_FOUND,
            $"No pipe network with id {input.Id} was found.",
            context.CorrelationId,
            context.SessionId));
    }
}
