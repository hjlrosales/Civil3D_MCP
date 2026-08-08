using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Autodesk.Mcp.Shared.Errors;
using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Domain.Surfaces.Services;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Query.Dtos;

namespace Civil3D.Tools.Query.Tools;

/// <summary>
/// Tool <c>get_surface</c>: returns a single surface by its stable numeric id. Read-only.
/// Fails with E_NO_ACTIVE_DOCUMENT when no drawing is open and E_OBJECT_NOT_FOUND when the id
/// does not exist.
/// </summary>
[McpTool(
    "get_surface",
    "Get Surface",
    "Returns a single surface by id. Read-only; fails with E_NO_ACTIVE_DOCUMENT when no drawing " +
    "is open and E_OBJECT_NOT_FOUND when the id does not exist.",
    Category = ToolCategory.Surfaces,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "surfaces", "lookup", "read-only" })]
public sealed class GetSurfaceTool : QueryToolBase<IdRequest, SurfaceInfo>
{
    private readonly ISurfaceService _surfaces;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="surfaces">The surface domain service.</param>
    public GetSurfaceTool(ICivil3DSession session, ISurfaceService surfaces)
        : base(session)
    {
        _surfaces = surfaces ?? throw new ArgumentNullException(nameof(surfaces));
    }

    /// <inheritdoc />
    protected override Task<SurfaceInfo> ExecuteToolCoreAsync(
        IdRequest input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireActiveDrawing(context);

        SurfaceInfo? result = RunQuery(context, () => _surfaces.GetById(input.Id));
        return Task.FromResult(result ?? throw new BridgeException(
            ErrorCode.E_OBJECT_NOT_FOUND,
            $"No surface with id {input.Id} was found.",
            context.CorrelationId,
            context.SessionId));
    }
}
