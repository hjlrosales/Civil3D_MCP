using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Civil3D.Domain.Data;
using Civil3D.Domain.Profiles.Dtos;

namespace Civil3D.Domain.Profiles.Data;

/// <summary>
/// Real <see cref="IProfileDataSource"/>: opens one read-only transaction through
/// <see cref="IAutodeskDocumentContext"/>, enumerates every alignment and its profiles via
/// <c>Alignment.GetProfileIds()</c>, and maps each profile to an immutable
/// <see cref="ProfileInfo"/>. No geometry analysis and no editing.
/// </summary>
public sealed class AutodeskProfileDataSource : IProfileDataSource
{
    private readonly IAutodeskDocumentContext _context;

    /// <summary>Creates the data source over the document context.</summary>
    /// <param name="context">The read-only transaction provider.</param>
    public AutodeskProfileDataSource(IAutodeskDocumentContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public ProfileCollection ReadAll(CancellationToken cancellationToken = default)
        => _context.ExecuteRead(
            database => ReadCore((Database)database, cancellationToken),
            cancellationToken);

    private static ProfileCollection ReadCore(Database database, CancellationToken cancellationToken)
    {
        using var transaction = database.TransactionManager.StartTransaction();
        CivilDocument civilDocument = CivilDocument.GetCivilDocument(database);
        ObjectIdCollection alignmentIds = civilDocument.GetAlignmentIds();

        var items = new List<ProfileInfo>();
        foreach (ObjectId alignmentId in alignmentIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var alignment = (Alignment)transaction.GetObject(alignmentId, OpenMode.ForRead);
            foreach (ObjectId profileId in alignment.GetProfileIds())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var profile = (Profile)transaction.GetObject(profileId, OpenMode.ForRead);
                items.Add(Map(profile));
            }
        }

        return new ProfileCollection(items);
    }

    private static ProfileInfo Map(Profile profile) => new()
    {
        Id = profile.ObjectId.Handle.Value,
        Name = profile.Name,
        Description = string.IsNullOrWhiteSpace(profile.Description) ? null : profile.Description,
        AlignmentId = profile.AlignmentId.Handle.Value,
        TypeName = profile.ProfileType.ToString(),
        Length = profile.Length,
        StartingStation = profile.StartingStation,
        EndingStation = profile.EndingStation,
        MinimumElevation = profile.ElevationMin,
        MaximumElevation = profile.ElevationMax,
    };
}
