using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Civil3D.Domain.Data;
using Civil3D.Domain.Styles.Dtos;

namespace Civil3D.Domain.Styles.Data;

/// <summary>
/// Real <see cref="IStyleDataSource"/>: opens one read-only transaction through
/// <see cref="IAutodeskDocumentContext"/>, enumerates the style collections under
/// <c>StylesRoot</c> (alignments, surfaces, corridors, pipes, structures, profiles, points and
/// feature lines) and maps every style to an immutable <see cref="StyleInfo"/>. Reads each style
/// once; no editing.
/// </summary>
public sealed class AutodeskStyleDataSource : IStyleDataSource
{
    private readonly IAutodeskDocumentContext _context;

    /// <summary>Creates the data source over the document context.</summary>
    /// <param name="context">The read-only transaction provider.</param>
    public AutodeskStyleDataSource(IAutodeskDocumentContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public StyleCollection ReadAll(CancellationToken cancellationToken = default)
        => _context.ExecuteRead(
            database => ReadCore((Database)database, cancellationToken),
            cancellationToken);

    private static StyleCollection ReadCore(Database database, CancellationToken cancellationToken)
    {
        using var transaction = database.TransactionManager.StartTransaction();
        CivilDocument civilDocument = CivilDocument.GetCivilDocument(database);
        StylesRoot styles = civilDocument.Styles;

        var items = new List<StyleInfo>();
        ReadCollection(transaction, styles.AlignmentStyles, StyleKind.Alignment, items, cancellationToken);
        ReadCollection(transaction, styles.SurfaceStyles, StyleKind.Surface, items, cancellationToken);
        ReadCollection(transaction, styles.CorridorStyles, StyleKind.Corridor, items, cancellationToken);
        ReadCollection(transaction, styles.PipeStyles, StyleKind.Pipe, items, cancellationToken);
        ReadCollection(transaction, styles.StructureStyles, StyleKind.Structure, items, cancellationToken);
        ReadCollection(transaction, styles.ProfileStyles, StyleKind.Profile, items, cancellationToken);
        ReadCollection(transaction, styles.PointStyles, StyleKind.Point, items, cancellationToken);
        ReadCollection(transaction, styles.FeatureLineStyles, StyleKind.FeatureLine, items, cancellationToken);

        return new StyleCollection(items);
    }

    private static void ReadCollection(
        Transaction transaction,
        StyleCollectionBase collection,
        StyleKind kind,
        List<StyleInfo> items,
        CancellationToken cancellationToken)
    {
        foreach (ObjectId id in collection)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var style = (StyleBase)transaction.GetObject(id, OpenMode.ForRead);
            items.Add(new StyleInfo
            {
                Id = id.Handle.Value,
                Name = style.Name,
                Description = string.IsNullOrWhiteSpace(style.Description) ? null : style.Description,
                Kind = kind,
            });
        }
    }
}
