using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Civil3D.Domain.Data;
using Civil3D.Domain.Pipes.Dtos;

namespace Civil3D.Domain.Pipes.Data;

/// <summary>
/// Real <see cref="IPipeDataSource"/>: opens one read-only transaction through
/// <see cref="IAutodeskDocumentContext"/>, enumerates <c>CivilDocument.GetPipeNetworkIds()</c> and
/// maps every network (with its pipes and structures via <c>Network.GetPipeIds()</c> and
/// <c>Network.GetStructureIds()</c>) to an immutable <see cref="PipeNetworkInfo"/>. No geometry
/// analysis and no editing.
/// </summary>
public sealed class AutodeskPipeDataSource : IPipeDataSource
{
    private readonly IAutodeskDocumentContext _context;

    /// <summary>Creates the data source over the document context.</summary>
    /// <param name="context">The read-only transaction provider.</param>
    public AutodeskPipeDataSource(IAutodeskDocumentContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public PipeNetworkCollection ReadAll(CancellationToken cancellationToken = default)
        => _context.ExecuteRead(
            database => ReadCore((Database)database, cancellationToken),
            cancellationToken);

    private static PipeNetworkCollection ReadCore(Database database, CancellationToken cancellationToken)
    {
        using var transaction = database.TransactionManager.StartTransaction();
        CivilDocument civilDocument = CivilDocument.GetCivilDocument(database);
        ObjectIdCollection ids = civilDocument.GetPipeNetworkIds();

        var items = new List<PipeNetworkInfo>(ids.Count);
        foreach (ObjectId id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var network = (Network)transaction.GetObject(id, OpenMode.ForRead);
            items.Add(Map(transaction, network, cancellationToken));
        }

        return new PipeNetworkCollection(items);
    }

    private static PipeNetworkInfo Map(Transaction transaction, Network network, CancellationToken cancellationToken)
    {
        return new PipeNetworkInfo
        {
            Id = network.ObjectId.Handle.Value,
            Name = network.Name,
            Description = string.IsNullOrWhiteSpace(network.Description) ? null : network.Description,
            PartsListName = string.IsNullOrWhiteSpace(network.PartsListName) ? null : network.PartsListName,
            Pipes = ReadPipes(transaction, network, cancellationToken),
            Structures = ReadStructures(transaction, network, cancellationToken),
        };
    }

    private static IReadOnlyList<PipeInfo> ReadPipes(Transaction transaction, Network network, CancellationToken cancellationToken)
    {
        long networkId = network.ObjectId.Handle.Value;
        ObjectIdCollection pipeIds = network.GetPipeIds();
        var pipes = new List<PipeInfo>(pipeIds.Count);
        foreach (ObjectId pipeId in pipeIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pipe = (Pipe)transaction.GetObject(pipeId, OpenMode.ForRead);
            pipes.Add(new PipeInfo
            {
                Id = pipe.ObjectId.Handle.Value,
                Name = pipe.Name,
                Description = string.IsNullOrWhiteSpace(pipe.Description) ? null : pipe.Description,
                NetworkId = networkId,
                StartStation = pipe.StartStation,
                EndStation = pipe.EndStation,
            });
        }

        return pipes;
    }

    private static IReadOnlyList<StructureInfo> ReadStructures(Transaction transaction, Network network, CancellationToken cancellationToken)
    {
        long networkId = network.ObjectId.Handle.Value;
        ObjectIdCollection structureIds = network.GetStructureIds();
        var structures = new List<StructureInfo>(structureIds.Count);
        foreach (ObjectId structureId in structureIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var structure = (Structure)transaction.GetObject(structureId, OpenMode.ForRead);
            structures.Add(new StructureInfo
            {
                Id = structure.ObjectId.Handle.Value,
                Name = structure.Name,
                Description = string.IsNullOrWhiteSpace(structure.Description) ? null : structure.Description,
                NetworkId = networkId,
                Easting = structure.Easting,
                Northing = structure.Northing,
                RimElevation = structure.RimElevation,
                SumpElevation = structure.SumpElevation,
            });
        }

        return structures;
    }
}
