using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Drawing.Dtos;

namespace Civil3D.Tools.Drawing.Tools;

/// <summary>
/// Tool <c>save_drawing</c>: persists the active drawing to its current path so newly created
/// objects (for example pipes) survive a restart, and by default zooms the current view to the
/// drawing extents so that geometry is immediately visible. Fails with E_NO_ACTIVE_DOCUMENT when
/// no drawing is open and E_TRANSACTION_FAILED when the drawing is read-only or has never been
/// saved (save it once from Civil 3D first).
/// </summary>
[McpTool(
    "save_drawing",
    "Save Drawing",
    "Saves the active drawing to its current path so newly created objects (for example pipes) " +
    "persist, and by default zooms the current view to the drawing extents so they are visible. " +
    "The save is queued and completes immediately after this tool returns. Fails with " +
    "E_NO_ACTIVE_DOCUMENT when no drawing is open and E_TRANSACTION_FAILED when the drawing is " +
    "read-only or has never been saved.",
    Category = ToolCategory.Drawing,
    Permission = ToolPermission.ModifyDrawing,
    Risk = ToolRisk.Medium,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "drawing", "save", "persist", "zoom" })]
public sealed class SaveDrawingTool : Civil3DToolBase<SaveDrawingRequest, SaveDrawingResult>
{
    private readonly ISaveDrawingService _save;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="save">Drawing save service.</param>
    public SaveDrawingTool(ICivil3DSession session, ISaveDrawingService save)
        : base(session)
    {
        _save = save ?? throw new ArgumentNullException(nameof(save));
    }

    /// <inheritdoc />
    protected override Task<SaveDrawingResult> ExecuteToolCoreAsync(
        SaveDrawingRequest input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ActiveDrawing drawing = RequireActiveDrawing(context);
        SaveDrawingResult result = _save.Save(drawing, input.ZoomExtents, cancellationToken);
        return Task.FromResult(result);
    }
}
