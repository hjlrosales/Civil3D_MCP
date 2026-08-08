using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Mcp.Shared.Errors;
using Civil3D.Tools.Abstractions;
using CoreApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Civil3D.Tools.Drawing.Services;

/// <summary>
/// Real <see cref="IDrawingStatisticsService"/> implementation. Opens a single read-only transaction
/// over the active database, reads every counter once (symbol tables by iteration, model/paper space
/// entity counts by block table record enumeration — never opening individual entities), commits and
/// disposes. No geometry analysis and no editing. Failures are mapped to <c>E_TRANSACTION_FAILED</c>.
/// </summary>
public sealed class AutodeskDrawingStatisticsService : IDrawingStatisticsService
{
    private const string ViewportClassName = "AcDbViewport";

    /// <inheritdoc />
    public DrawingStatistics GetStatistics(ActiveDrawing drawing, CancellationToken cancellationToken)
    {
        Document? document = CoreApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            throw new BridgeException(ErrorCode.E_NO_ACTIVE_DOCUMENT, "No active document is available to operate on.");
        }

        Database database = document.Database;
        try
        {
            using Transaction transaction = database.TransactionManager.StartTransaction();

            int layerCount = CountSymbolTable(transaction, database.LayerTableId);
            cancellationToken.ThrowIfCancellationRequested();

            int blockCount = CountSymbolTable(transaction, database.BlockTableId);
            (int modelCount, int paperCount, int viewportCount, int xrefCount) = CountSpaces(transaction, database);
            cancellationToken.ThrowIfCancellationRequested();

            int textStyleCount = CountSymbolTable(transaction, database.TextStyleTableId);
            int dimensionStyleCount = CountSymbolTable(transaction, database.DimStyleTableId);
            int linetypeCount = CountSymbolTable(transaction, database.LinetypeTableId);
            int registeredApplicationCount = CountSymbolTable(transaction, database.RegAppTableId);
            int dictionaryCount = CountNamedObjects(transaction, database);
            long approximateSizeBytes = ApproximateFileSize(database);

            transaction.Commit();

            return new DrawingStatistics
            {
                LayerCount = layerCount,
                BlockCount = blockCount,
                XRefCount = xrefCount,
                EntityCount = modelCount + paperCount,
                ModelSpaceEntityCount = modelCount,
                PaperSpaceEntityCount = paperCount,
                ViewportCount = viewportCount,
                TextStyleCount = textStyleCount,
                DimensionStyleCount = dimensionStyleCount,
                LinetypeCount = linetypeCount,
                RegisteredApplicationCount = registeredApplicationCount,
                DictionaryCount = dictionaryCount,
                ApproximateDrawingSizeBytes = approximateSizeBytes,
            };
        }
        catch (BridgeException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new BridgeException(
                ErrorCode.E_TRANSACTION_FAILED,
                "The read-only drawing statistics scan failed.",
                innerException: ex);
        }
    }

    private static int CountSymbolTable(Transaction transaction, ObjectId tableId)
    {
        if (tableId.IsNull)
        {
            return 0;
        }

        var table = (SymbolTable)transaction.GetObject(tableId, OpenMode.ForRead);
        int count = 0;
        foreach (ObjectId _ in table)
        {
            count++;
        }

        return count;
    }

    private static (int Model, int Paper, int Viewports, int XRefs) CountSpaces(Transaction transaction, Database database)
    {
        var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
        ObjectId modelSpaceId = blockTable[BlockTableRecord.ModelSpace];

        int model = CountEntities(transaction, modelSpaceId);
        int paper = 0;
        int viewports = 0;
        int xrefs = 0;

        foreach (ObjectId recordId in blockTable)
        {
            // The model space record is counted separately above; never let it leak into paper space.
            if (recordId == modelSpaceId)
            {
                continue;
            }

            var record = (BlockTableRecord)transaction.GetObject(recordId, OpenMode.ForRead);
            if (record.IsFromExternalReference)
            {
                xrefs++;
            }

            if (!record.IsLayout)
            {
                continue;
            }

            paper += CountEntities(transaction, recordId);
            viewports += CountViewports(transaction, recordId);
        }

        return (model, paper, viewports, xrefs);
    }

    private static int CountEntities(Transaction transaction, ObjectId blockTableRecordId)
    {
        var record = (BlockTableRecord)transaction.GetObject(blockTableRecordId, OpenMode.ForRead);
        int count = 0;
        foreach (ObjectId _ in record)
        {
            count++;
        }

        return count;
    }

    private static int CountViewports(Transaction transaction, ObjectId blockTableRecordId)
    {
        var record = (BlockTableRecord)transaction.GetObject(blockTableRecordId, OpenMode.ForRead);
        int count = 0;
        foreach (ObjectId id in record)
        {
            // Classification via the runtime class is cheap and avoids opening every entity.
            if (id.ObjectClass.Name == ViewportClassName)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountNamedObjects(Transaction transaction, Database database)
    {
        if (database.NamedObjectsDictionaryId.IsNull)
        {
            return 0;
        }

        var dictionaries = (DBDictionary)transaction.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForRead);
        return dictionaries.Count;
    }

    private static long ApproximateFileSize(Database database)
    {
        string filename = database.Filename;
        if (string.IsNullOrWhiteSpace(filename) || !File.Exists(filename))
        {
            return 0;
        }

        try
        {
            return new FileInfo(filename).Length;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
