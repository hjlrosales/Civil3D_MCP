using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Civil3D.Domain.Data;
using Civil3D.Domain.Surfaces.Dtos;
using CivilSurface = Autodesk.Civil.DatabaseServices.Surface;

namespace Civil3D.Domain.Surfaces.Data;

/// <summary>
/// Real <see cref="ISurfaceDataSource"/>: opens one read-only transaction through
/// <see cref="IAutodeskDocumentContext"/>, enumerates <c>CivilDocument.GetSurfaceIds()</c> and
/// maps every surface (plus cheap general properties) to an immutable <see cref="SurfaceInfo"/>.
/// </summary>
public sealed class AutodeskSurfaceDataSource : ISurfaceDataSource
{
    private readonly IAutodeskDocumentContext _context;

    /// <summary>Creates the data source over the document context.</summary>
    /// <param name="context">The read-only transaction provider.</param>
    public AutodeskSurfaceDataSource(IAutodeskDocumentContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public SurfaceCollection ReadAll(CancellationToken cancellationToken = default)
        => _context.ExecuteRead(
            database => ReadCore((Database)database, cancellationToken),
            cancellationToken);

    private static SurfaceCollection ReadCore(Database database, CancellationToken cancellationToken)
    {
        using var transaction = database.TransactionManager.StartTransaction();
        CivilDocument civilDocument = CivilDocument.GetCivilDocument(database);
        ObjectIdCollection ids = civilDocument.GetSurfaceIds();

        var items = new List<SurfaceInfo>(ids.Count);
        foreach (ObjectId id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var surface = (CivilSurface)transaction.GetObject(id, OpenMode.ForRead);
            items.Add(Map(surface));
        }

        return new SurfaceCollection(items);
    }

    private static SurfaceInfo Map(CivilSurface surface)
    {
        GeneralSurfaceProperties properties = surface.GetGeneralProperties();
        return new SurfaceInfo
        {
            Id = surface.ObjectId.Handle.Value,
            Name = surface.Name,
            Description = string.IsNullOrWhiteSpace(surface.Description) ? null : surface.Description,
            Kind = Classify(surface),
            PointCount = properties.NumberOfPoints,
            MinimumElevation = properties.MinimumElevation,
            MaximumElevation = properties.MaximumElevation,
            MeanElevation = properties.MeanElevation,
        };
    }

    private static SurfaceKind Classify(CivilSurface surface) => surface switch
    {
        TinSurface => SurfaceKind.Tin,
        GridSurface => SurfaceKind.Grid,
        TinVolumeSurface => SurfaceKind.TinVolume,
        GridVolumeSurface => SurfaceKind.GridVolume,
        _ => SurfaceKind.Other,
    };
}
