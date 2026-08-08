using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Autodesk.Mcp.Shared.Errors;
using Civil3D.Domain.Styles.Dtos;
using Civil3D.Domain.Styles.Services;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Query.Dtos;

namespace Civil3D.Tools.Query.Tools;

/// <summary>
/// Tool <c>get_style</c>: returns a single Civil 3D style by its stable numeric id. Read-only.
/// Fails with E_NO_ACTIVE_DOCUMENT when no drawing is open and E_OBJECT_NOT_FOUND when the id
/// does not exist.
/// </summary>
[McpTool(
    "get_style",
    "Get Style",
    "Returns a single Civil 3D style by id. Read-only; fails with E_NO_ACTIVE_DOCUMENT when no " +
    "drawing is open and E_OBJECT_NOT_FOUND when the id does not exist.",
    Category = ToolCategory.Styles,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "styles", "lookup", "read-only" })]
public sealed class GetStyleTool : QueryToolBase<IdRequest, StyleInfo>
{
    private readonly IStyleService _styles;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="styles">The style domain service.</param>
    public GetStyleTool(ICivil3DSession session, IStyleService styles)
        : base(session)
    {
        _styles = styles ?? throw new ArgumentNullException(nameof(styles));
    }

    /// <inheritdoc />
    protected override Task<StyleInfo> ExecuteToolCoreAsync(
        IdRequest input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireActiveDrawing(context);

        StyleInfo? result = RunQuery(context, () => _styles.GetById(input.Id));
        return Task.FromResult(result ?? throw new BridgeException(
            ErrorCode.E_OBJECT_NOT_FOUND,
            $"No style with id {input.Id} was found.",
            context.CorrelationId,
            context.SessionId));
    }
}
