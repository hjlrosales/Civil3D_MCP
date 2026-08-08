using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Data;

namespace Civil3D.Domain.Alignments.Data;

/// <summary>
/// Real <see cref="IAlignmentDataSource"/>: opens one read-only transaction through
/// <see cref="IAutodeskDocumentContext"/>, enumerates <c>CivilDocument.GetAlignmentIds()</c>,
/// maps every alignment to an immutable <see cref="AlignmentInfo"/> and returns. Reads each
/// alignment exactly once; no geometry analysis and no editing.
/// </summary>
public sealed class AutodeskAlignmentDataSource : IAlignmentDataSource
{
    private readonly IAutodeskDocumentContext _context;

    /// <summary>Creates the data source over the document context.</summary>
    /// <param name="context">The read-only transaction provider.</param>
    public AutodeskAlignmentDataSource(IAutodeskDocumentContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public AlignmentCollection ReadAll(CancellationToken cancellationToken = default)
        => _context.ExecuteRead(
            database => ReadCore((Database)database, cancellationToken),
            cancellationToken);

    private static AlignmentCollection ReadCore(Database database, CancellationToken cancellationToken)
    {
        using var transaction = database.TransactionManager.StartTransaction();
        CivilDocument civilDocument = CivilDocument.GetCivilDocument(database);
        ObjectIdCollection ids = civilDocument.GetAlignmentIds();

        var items = new List<AlignmentInfo>(ids.Count);
        foreach (ObjectId id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var alignment = (Alignment)transaction.GetObject(id, OpenMode.ForRead);
            items.Add(Map(alignment));
        }

        return new AlignmentCollection(items);
    }

    private static AlignmentInfo Map(Alignment alignment) => new()
    {
        Id = alignment.ObjectId.Handle.Value,
        Name = alignment.Name,
        Description = string.IsNullOrWhiteSpace(alignment.Description) ? null : alignment.Description,
        Kind = MapKind(alignment.AlignmentType),
        Length = alignment.Length,
        StartingStation = alignment.StartingStation,
        EndingStation = alignment.EndingStation,
        SiteId = NullableId(alignment.SiteId),
        StyleId = NullableId(alignment.StyleId),
    };

    private static AlignmentKind MapKind(AlignmentType type) => type switch
    {
        AlignmentType.Centerline => AlignmentKind.Centerline,
        AlignmentType.Offset => AlignmentKind.Offset,
        AlignmentType.CurbReturn => AlignmentKind.CurbReturn,
        AlignmentType.Utility => AlignmentKind.Utility,
        AlignmentType.Rail => AlignmentKind.Rail,
        _ => AlignmentKind.Other,
    };

    private static long? NullableId(ObjectId id) => id.IsNull ? null : id.Handle.Value;
}
