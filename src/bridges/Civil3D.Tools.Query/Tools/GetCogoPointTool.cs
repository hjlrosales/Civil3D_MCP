using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Autodesk.Mcp.Shared.Errors;
using Civil3D.Domain.Cogo.Dtos;
using Civil3D.Domain.Cogo.Services;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Query.Dtos;

namespace Civil3D.Tools.Query.Tools;

/// <summary>
/// Tool <c>get_cogo_point</c>: returns a single COGO point by its stable numeric id. Read-only.
/// Fails with E_NO_ACTIVE_DOCUMENT when no drawing is open and E_OBJECT_NOT_FOUND when the id
/// does not exist.
/// </summary>
[McpTool(
    "get_cogo_point",
    "Get COGO Point",
    "Returns a single COGO point by id. Read-only; fails with E_NO_ACTIVE_DOCUMENT when no " +
    "drawing is open and E_OBJECT_NOT_FOUND when the id does not exist.",
    Category = ToolCategory.Cogo,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "cogo", "points", "lookup", "read-only" })]
public sealed class GetCogoPointTool : QueryToolBase<IdRequest, CogoPointInfo>
{
    private readonly ICogoService _cogo;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="cogo">The COGO domain service.</param>
    public GetCogoPointTool(ICivil3DSession session, ICogoService cogo)
        : base(session)
    {
        _cogo = cogo ?? throw new ArgumentNullException(nameof(cogo));
    }

    /// <inheritdoc />
    protected override Task<CogoPointInfo> ExecuteToolCoreAsync(
        IdRequest input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireActiveDrawing(context);

        CogoPointInfo? result = RunQuery(context, () => _cogo.GetById(input.Id));
        return Task.FromResult(result ?? throw new BridgeException(
            ErrorCode.E_OBJECT_NOT_FOUND,
            $"No COGO point with id {input.Id} was found.",
            context.CorrelationId,
            context.SessionId));
    }
}
