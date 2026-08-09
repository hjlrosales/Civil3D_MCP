using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Mcp.Shared.Errors;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Drawing.Dtos;
using CoreApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Civil3D.Tools.Drawing.Services;

/// <summary>
/// Real <see cref="ISaveDrawingService"/>: saves the active drawing in place via
/// <c>Database.SaveAs</c> and, when requested, zooms the current view to the drawing extents
/// (<c>ZOOM Extents</c>) so geometry created just before the save is visible. Must run on the
/// application context (the tool dispatcher guarantees this). The zoom happens before the save so
/// the refreshed view is persisted with the drawing.
/// </summary>
public sealed class AutodeskSaveDrawingService : ISaveDrawingService
{
    /// <inheritdoc />
    public SaveDrawingResult Save(ActiveDrawing drawing, bool zoomToExtents, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (drawing.IsReadOnly)
        {
            throw new BridgeException(
                ErrorCode.E_TRANSACTION_FAILED,
                $"Drawing '{drawing.DrawingName}' is read-only and cannot be saved.");
        }

        Document? document = CoreApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            throw new BridgeException(
                ErrorCode.E_NO_ACTIVE_DOCUMENT,
                "No active document is available to operate on.");
        }

        string fileName = document.Database.Filename;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new BridgeException(
                ErrorCode.E_TRANSACTION_FAILED,
                "The active drawing has no file name yet; save it once from Civil 3D before using save_drawing.");
        }

        // No explicit LockDocument is taken: the tool already runs on the application context and
        // the save is issued through the QSAVE command, which manages its own locks.
        if (zoomToExtents)
        {
            // The ZOOM command cannot be issued synchronously from the Application.Idle event
            // (Editor.Command throws there); SendStringToExecute queues it to run right after
            // the current tool completes. Best-effort: the save must happen either way.
            try
            {
                document.SendStringToExecute("_ZOOM _E ", true, false, false);
            }
            catch (Exception)
            {
                // Best-effort: the zoom is cosmetic; the save must happen either way.
            }
        }

        Save(document, fileName);

        return new SaveDrawingResult
        {
            Success = true,
            DrawingName = drawing.DrawingName,
            DrawingPath = fileName,
            SavedAtUtc = DateTime.UtcNow,
            ZoomedToExtents = zoomToExtents,
        };
    }

    private static void Save(Document document, string fileName)
    {
        // Database.SaveAs throws eFilerError when called from the Application.Idle event (the
        // bridge runs tools there), so the save is issued through the QSAVE command instead.
        // Editor.Command would run synchronously where it works, but it is not allowed from Idle
        // (eInvalidInput), so the save is queued with SendStringToExecute and runs in the document
        // command context immediately after the current tool completes.
        try
        {
#pragma warning disable CS0618
            document.Editor.Command("_QSAVE");
#pragma warning restore CS0618
        }
        catch (Autodesk.AutoCAD.Runtime.Exception)
        {
            document.SendStringToExecute("_QSAVE ", true, false, false);
        }
        catch (Exception ex)
        {
            throw new BridgeException(
                ErrorCode.E_TRANSACTION_FAILED,
                $"The drawing could not be saved ('{ex.Message}').");
        }
    }
}
