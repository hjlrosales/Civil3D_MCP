using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Domain.Data;
using AutodeskCorridorCollection = Autodesk.Civil.DatabaseServices.CorridorCollection;
using CorridorCollection = Civil3D.Domain.Corridors.Dtos.CorridorCollection;

namespace Civil3D.Domain.Corridors.Data;

/// <summary>
/// Real <see cref="ICorridorDataSource"/>: opens one read-only transaction through
/// <see cref="IAutodeskDocumentContext"/>, enumerates <c>CivilDocument.CorridorCollection</c> and
/// maps every corridor to an immutable <see cref="CorridorInfo"/>. Reads each corridor exactly
/// once; no geometry analysis and no editing.
/// </summary>
public sealed class AutodeskCorridorDataSource : ICorridorDataSource
{
    private readonly IAutodeskDocumentContext _context;

    /// <summary>Creates the data source over the document context.</summary>
    /// <param name="context">The read-only transaction provider.</param>
    public AutodeskCorridorDataSource(IAutodeskDocumentContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public CorridorCollection ReadAll(CancellationToken cancellationToken = default)
        => _context.ExecuteRead(
            database => ReadCore((Database)database, cancellationToken),
            cancellationToken);

    private static CorridorCollection ReadCore(Database database, CancellationToken cancellationToken)
    {
        using var transaction = database.TransactionManager.StartTransaction();
        CivilDocument civilDocument = CivilDocument.GetCivilDocument(database);
        AutodeskCorridorCollection collection = civilDocument.CorridorCollection;

        var items = new List<CorridorInfo>(collection.Count);
        foreach (ObjectId id in collection)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var corridor = (Corridor)transaction.GetObject(id, OpenMode.ForRead);
            items.Add(Map(corridor));
        }

        return new CorridorCollection(items);
    }

    private static CorridorInfo Map(Corridor corridor) => new()
    {
        Id = corridor.ObjectId.Handle.Value,
        Name = corridor.Name,
        Description = string.IsNullOrWhiteSpace(corridor.Description) ? null : corridor.Description,
        StyleId = NullableId(corridor.StyleId),
        CodeSetStyleId = NullableId(corridor.CodeSetStyleId),
        AlignmentId = PrimaryAlignmentId(corridor),
        BaselineCount = corridor.Baselines.Count,
        CorridorSurfaceCount = corridor.CorridorSurfaces.Count,
    };

    private static long? PrimaryAlignmentId(Corridor corridor)
    {
        if (corridor.Baselines.Count == 0)
        {
            return null;
        }

        Baseline first = corridor.Baselines[0];
        return first.AlignmentId.IsNull ? null : first.AlignmentId.Handle.Value;
    }

    private static long? NullableId(ObjectId id) => id.IsNull ? null : id.Handle.Value;
}
