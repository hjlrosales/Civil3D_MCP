using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Autodesk.Mcp.Shared.Errors;
using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Domain.Corridors.Services;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Query.Dtos;

namespace Civil3D.Tools.Query.Tools;

/// <summary>
/// Tool <c>get_corridor</c>: returns a single corridor by its stable numeric id. Read-only.
/// Fails with E_NO_ACTIVE_DOCUMENT when no drawing is open and E_OBJECT_NOT_FOUND when the id
/// does not exist.
/// </summary>
[McpTool(
    "get_corridor",
    "Get Corridor",
    "Returns a single corridor by id. Read-only; fails with E_NO_ACTIVE_DOCUMENT when no drawing " +
    "is open and E_OBJECT_NOT_FOUND when the id does not exist.",
    Category = ToolCategory.Corridors,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "corridors", "lookup", "read-only" })]
public sealed class GetCorridorTool : QueryToolBase<IdRequest, CorridorInfo>
{
    private readonly ICorridorService _corridors;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="corridors">The corridor domain service.</param>
    public GetCorridorTool(ICivil3DSession session, ICorridorService corridors)
        : base(session)
    {
        _corridors = corridors ?? throw new ArgumentNullException(nameof(corridors));
    }

    /// <inheritdoc />
    protected override Task<CorridorInfo> ExecuteToolCoreAsync(
        IdRequest input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireActiveDrawing(context);

        CorridorInfo? result = RunQuery(context, () => _corridors.GetById(input.Id));
        return Task.FromResult(result ?? throw new BridgeException(
            ErrorCode.E_OBJECT_NOT_FOUND,
            $"No corridor with id {input.Id} was found.",
            context.CorrelationId,
            context.SessionId));
    }
}
