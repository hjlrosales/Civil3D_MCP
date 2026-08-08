using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Civil3D.Tools.Abstractions;
using CoreApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Civil3D.Tools.Drawing.Services;

/// <summary>
/// Real <see cref="ICivil3DSession"/> implementation: reads the active document from
/// <c>Application.DocumentManager</c>. Must only be invoked on the application context (the tool
/// dispatcher guarantees this for <see cref="Civil3DToolBase{TIn,TOut}"/>-derived tools). The
/// snapshot is read once per call and contains no live Autodesk object references.
/// </summary>
public sealed class AutodeskCivil3DSession : ICivil3DSession
{
    /// <inheritdoc />
    public ActiveDrawing? GetActiveDrawing()
    {
        DocumentCollection documents = CoreApplication.DocumentManager;
        Document? document = documents.MdiActiveDocument;
        if (document is null)
        {
            return null;
        }

        Database database = document.Database;
        return new ActiveDrawing
        {
            DrawingName = Path.GetFileName(document.Name),
            DrawingPath = document.Name,
            DrawingVersion = database.OriginalFileSavedByVersion.ToString(),
            IsModified = HasUnsavedChanges(),
            IsReadOnly = document.IsReadOnly,
            CurrentLayout = LayoutManager.Current.CurrentLayout,
            IsModelSpaceActive = database.TileMode,
            DatabaseFingerprint = database.FingerprintGuid,
            Civil3DVersion = CoreApplication.Version.ToString(),
            OpenDocumentsCount = documents.Count,
            CurrentDocumentName = Path.GetFileName(document.Name),
            CurrentDocumentPath = document.Name,
        };
    }

    private static bool HasUnsavedChanges()
    {
        // DBMOD is the sum of the modified-state flags: 1 objects, 2 symbol tables, 4 database
        // variables (content), plus 8 window and 16 view (cosmetic). Only the content bits are
        // relevant to "does the drawing contain unsaved changes"; the document manager resets them
        // on save.
        try
        {
            short dbMod = Convert.ToInt16(CoreApplication.GetSystemVariable("DBMOD"));
            return (dbMod & 0x7) != 0;
        }
        catch (Autodesk.AutoCAD.Runtime.Exception)
        {
            // The variable is unavailable (for example during startup); treat as unknown.
            return false;
        }
    }
}
