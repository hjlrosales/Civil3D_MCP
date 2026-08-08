using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Civil3D.Domain.Cogo.Dtos;
using Civil3D.Domain.Data;
using CogoPointCollection = Civil3D.Domain.Cogo.Dtos.CogoPointCollection;

namespace Civil3D.Domain.Cogo.Data;

/// <summary>
/// Real <see cref="ICogoDataSource"/>: opens one read-only transaction through
/// <see cref="IAutodeskDocumentContext"/>, enumerates <c>CivilDocument.GetAllPointIds()</c> and
/// maps every point to an immutable <see cref="CogoPointInfo"/>. Reads each point exactly once.
/// </summary>
public sealed class AutodeskCogoDataSource : ICogoDataSource
{
    private readonly IAutodeskDocumentContext _context;

    /// <summary>Creates the data source over the document context.</summary>
    /// <param name="context">The read-only transaction provider.</param>
    public AutodeskCogoDataSource(IAutodeskDocumentContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public CogoPointCollection ReadAll(CancellationToken cancellationToken = default)
        => _context.ExecuteRead(
            database => ReadCore((Database)database, cancellationToken),
            cancellationToken);

    private static CogoPointCollection ReadCore(Database database, CancellationToken cancellationToken)
    {
        using var transaction = database.TransactionManager.StartTransaction();
        CivilDocument civilDocument = CivilDocument.GetCivilDocument(database);
        ObjectIdCollection ids = civilDocument.GetAllPointIds();

        var items = new List<CogoPointInfo>(ids.Count);
        foreach (ObjectId id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var point = (CogoPoint)transaction.GetObject(id, OpenMode.ForRead);
            items.Add(new CogoPointInfo
            {
                Id = point.ObjectId.Handle.Value,
                PointNumber = point.PointNumber,
                Easting = point.Easting,
                Northing = point.Northing,
                Elevation = point.Elevation,
                FullDescription = string.IsNullOrWhiteSpace(point.FullDescription) ? null : point.FullDescription,
                IsLocked = point.IsLocked,
            });
        }

        return new CogoPointCollection(items);
    }
}
