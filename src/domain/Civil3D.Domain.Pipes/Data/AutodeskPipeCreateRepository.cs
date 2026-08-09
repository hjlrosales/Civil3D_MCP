using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Data;
using Civil3D.Domain.Errors;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Pipes.Repositories;

namespace Civil3D.Domain.Pipes.Data;

/// <summary>
/// Real <see cref="IPipeCreateRepository"/>: opens the target network for write inside the active
/// transaction, resolves the pipe part family from the network's parts list by matching
/// <see cref="CreatePipeSpecification.PartFamilyMatch"/> against family descriptions
/// (case-insensitive substring; the match must be unique), adds a straight pipe along the given
/// line via <c>Network.AddLinePipe</c>, and snaps it to the closest available size to the
/// requested diameter via <c>Pipe.ResizeByInnerDiameterOrWidth</c>. No business rules beyond part
/// resolution — the create service validates the network exists before this is called.
/// </summary>
public sealed class AutodeskPipeCreateRepository : IPipeCreateRepository
{
    private readonly IAutodeskDocumentContext _context;

    /// <summary>Creates the repository over the document context.</summary>
    /// <param name="context">Resolves the active drawing database.</param>
    public AutodeskPipeCreateRepository(IAutodeskDocumentContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public CreatePipeOutcome Create(IWriteTransaction transaction, long networkId, CreatePipeSpecification specification)
        => _context.ExecuteWrite(
            database => CreateCore((Database)database, transaction, networkId, specification));

    private static CreatePipeOutcome CreateCore(
        Database database, IWriteTransaction transaction, long networkId, CreatePipeSpecification specification)
    {
        if (transaction.Handle is not Transaction tx)
        {
            throw new DomainException(
                DomainErrorCode.TransactionFailed,
                "The active transaction is not an AutoCAD database transaction.");
        }

        Network network = OpenNetworkForWrite(database, tx, networkId);
        (ObjectId familyId, string familyDescription) = ResolvePartFamily(tx, network, specification.PartFamilyMatch, specification.FallbackMatch);
        ObjectId sizeId = ResolveInitialSize(tx, familyId, familyDescription);

        var line = new LineSegment3d(
            new Point3d(specification.StartEasting, specification.StartNorthing, specification.StartElevation),
            new Point3d(specification.EndEasting, specification.EndNorthing, specification.EndElevation));

        ObjectId newPipeId = ObjectId.Null;
        try
        {
            network.AddLinePipe(familyId, sizeId, line, ref newPipeId, applyRules: true);
        }
        catch (Autodesk.AutoCAD.Runtime.Exception ex)
        {
            throw new DomainException(
                DomainErrorCode.TransactionFailed,
                $"Civil 3D rejected the new pipe geometry in network '{network.Name}'.", ex);
        }

        var pipe = (Pipe)tx.GetObject(newPipeId, OpenMode.ForWrite);
        pipe.ResizeByInnerDiameterOrWidth(specification.DiameterMm, useClosestSize: true);
        if (!string.IsNullOrWhiteSpace(specification.Description))
        {
            pipe.Description = specification.Description;
        }

        return new CreatePipeOutcome
        {
            PipeId = pipe.ObjectId.Handle.Value,
            Name = pipe.Name,
            NetworkId = networkId,
            NetworkName = network.Name,
            PartFamilyName = familyDescription,
            PartSizeName = pipe.PartSizeName,
            Material = string.IsNullOrWhiteSpace(pipe.Material) ? null : pipe.Material,
            InnerDiameterOrWidth = pipe.InnerDiameterOrWidth,
            OuterDiameterOrWidth = pipe.OuterDiameterOrWidth,
            StartEasting = pipe.StartPoint.X,
            StartNorthing = pipe.StartPoint.Y,
            StartElevation = pipe.StartPoint.Z,
            EndEasting = pipe.EndPoint.X,
            EndNorthing = pipe.EndPoint.Y,
            EndElevation = pipe.EndPoint.Z,
            Length3D = pipe.Length3D,
        };
    }

    private static Network OpenNetworkForWrite(Database database, Transaction tx, long networkId)
    {
        try
        {
            ObjectId networkObjectId = database.GetObjectId(false, new Handle(networkId), 0);
            return (Network)tx.GetObject(networkObjectId, OpenMode.ForWrite);
        }
        catch (Autodesk.AutoCAD.Runtime.Exception ex)
        {
            throw new DomainException(
                DomainErrorCode.EntityNotFound,
                $"The pipe network with id {networkId} could not be opened for write.", ex);
        }
    }

    private static (ObjectId Id, string Description) ResolvePartFamily(Transaction tx, Network network, string partFamilyMatch, string? fallbackMatch = null)
    {
        if (network.PartsListId.IsNull)
        {
            throw new DomainException(
                DomainErrorCode.PartNotFound,
                $"Network '{network.Name}' has no parts list assigned; assign one in Civil 3D before creating pipes.");
        }

        var partsList = (PartsList)tx.GetObject(network.PartsListId, OpenMode.ForRead);
        ObjectIdCollection familyIds = partsList.GetPartFamilyIdsByDomain(DomainType.Pipe);

        var matches = new List<(ObjectId Id, string Description)>();
        var available = new List<string>();
        foreach (ObjectId familyId in familyIds)
        {
            var family = (PartFamily)tx.GetObject(familyId, OpenMode.ForRead);
            available.Add(family.Description);
            if (family.Description.Contains(partFamilyMatch, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add((familyId, family.Description));
            }
        }

        if (matches.Count == 0 && !string.IsNullOrWhiteSpace(fallbackMatch))
        {
            // Material/rating prompts (for example "HDPE SDR17 PN10") often reference ratings the
            // drawing's catalog does not name; retry with the bare material when it was provided.
            foreach (ObjectId familyId in familyIds)
            {
                var family = (PartFamily)tx.GetObject(familyId, OpenMode.ForRead);
                if (family.Description.Contains(fallbackMatch, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add((familyId, family.Description));
                }
            }
        }

        if (matches.Count == 0)
        {
            throw new DomainException(
                DomainErrorCode.PartNotFound,
                $"No pipe part family in network '{network.Name}' matches '{partFamilyMatch}'. " +
                $"Available families: {(available.Count == 0 ? "(none)" : string.Join(", ", available))}.");
        }

        if (matches.Count > 1)
        {
            throw new DomainException(
                DomainErrorCode.PartNotFound,
                $"'{partFamilyMatch}' matches more than one pipe part family in network '{network.Name}': " +
                $"{string.Join(", ", matches.Select(m => m.Description))}. Use a more specific match.");
        }

        return matches[0];
    }

    private static ObjectId ResolveInitialSize(Transaction tx, ObjectId familyId, string familyDescription)
    {
        var family = (PartFamily)tx.GetObject(familyId, OpenMode.ForRead);
        if (family.PartSizeCount == 0)
        {
            throw new DomainException(
                DomainErrorCode.PartNotFound,
                $"Part family '{familyDescription}' has no sizes defined.");
        }

        // Any size works as the creation seed: ResizeByInnerDiameterOrWidth (called on the new
        // pipe right after creation) snaps to the size closest to the requested diameter.
        return family[0];
    }
}
