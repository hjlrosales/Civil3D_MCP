using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Commands;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Dtos;
using Civil3D.Domain.Errors;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Pipes.Repositories;
using Civil3D.Domain.Query;
using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Domain.Alignments.Repositories;
using Civil3D.Domain.Surfaces.Repositories;

namespace Civil3D.Tools.Editing.Tests;

/// <summary>
/// An in-memory Civil 3D drawing for the rename and create-pipe tests: alignment and surface name
/// tables, a pipe network catalog (networks with their assigned pipe part families/sizes), plus a
/// write transaction whose <see cref="IWriteTransaction.Handle"/> is a typed handle the write
/// repositories can open for write. Stands in for the Autodesk database/transaction.
/// </summary>
internal sealed class InMemoryDrawing
{
    internal sealed class Entry(long id, string name)
    {
        public long Id { get; } = id;
        public string Name { get; set; } = name;
    }

    /// <summary>A pipe part size available within a <see cref="FakePartFamily"/>.</summary>
    internal sealed class FakePartSize(string name, double diameterMm)
    {
        public string Name { get; } = name;
        public double DiameterMm { get; } = diameterMm;
    }

    /// <summary>A pipe part family assigned to a <see cref="FakeNetwork"/>'s parts list.</summary>
    internal sealed class FakePartFamily(string description, params double[] sizesMm)
    {
        public string Description { get; } = description;
        public List<FakePartSize> Sizes { get; } =
            sizesMm.Select(mm => new FakePartSize($"{mm:0.#} mm", mm)).ToList();
    }

    /// <summary>A pipe created inside a <see cref="FakeNetwork"/> by the create-pipe write repository.</summary>
    internal sealed class FakePipe(long id, string name, string partFamilyDescription, FakePartSize size)
    {
        public long Id { get; } = id;
        public string Name { get; } = name;
        public string PartFamilyDescription { get; } = partFamilyDescription;
        public FakePartSize Size { get; } = size;
        public double StartEasting { get; set; }
        public double StartNorthing { get; set; }
        public double StartElevation { get; set; }
        public double EndEasting { get; set; }
        public double EndNorthing { get; set; }
        public double EndElevation { get; set; }
        public double Length3D { get; set; }
        public string? Description { get; set; }
    }

    /// <summary>A pipe network with the pipe part families available in its parts list.</summary>
    internal sealed class FakeNetwork(long id, string name, params FakePartFamily[] partFamilies)
    {
        public long Id { get; } = id;
        public string Name { get; } = name;
        public List<FakePartFamily> PartFamilies { get; } = partFamilies.ToList();
        public List<FakePipe> Pipes { get; } = [];
    }

    /// <summary>A fake write transaction bound to the in-memory drawing; commit marks success.</summary>
    internal sealed class InMemoryWriteTransaction : IWriteTransaction
    {
        private readonly InMemoryDrawing _drawing;

        public InMemoryWriteTransaction(InMemoryDrawing drawing) => _drawing = drawing;

        public object? Handle => _drawing;
        public bool IsCommitted { get; private set; }
        public bool IsRolledBack { get; private set; }
        public bool IsDisposed { get; private set; }
        public bool WasModified { get; private set; }

        public void Commit()
        {
            if (IsCommitted)
            {
                throw new CommandException(CommandErrorCode.TransactionFailed, "Commit after commit.");
            }

            IsCommitted = true;
        }

        public void Rollback()
        {
            if (IsCommitted)
            {
                throw new CommandException(CommandErrorCode.TransactionFailed, "Rollback after commit.");
            }

            IsRolledBack = true;
        }

        public void Dispose() => IsDisposed = true;
    }

    private readonly List<Entry> _alignments;
    private readonly List<Entry> _surfaces;
    private readonly List<FakeNetwork> _networks;
    private long _nextPipeId = 1000;

    public IReadOnlyList<Entry> Alignments => _alignments;
    public IReadOnlyList<Entry> Surfaces => _surfaces;
    public IReadOnlyList<FakeNetwork> Networks => _networks;

    public InMemoryDrawing(
        IEnumerable<(long Id, string Name)>? alignments = null,
        IEnumerable<(long Id, string Name)>? surfaces = null,
        IEnumerable<FakeNetwork>? networks = null)
    {
        _alignments = (alignments ?? []).Select(a => new Entry(a.Id, a.Name)).ToList();
        _surfaces = (surfaces ?? []).Select(s => new Entry(s.Id, s.Name)).ToList();
        _networks = (networks ?? []).ToList();
    }

    public Entry? FindAlignment(long id) => _alignments.FirstOrDefault(a => a.Id == id);
    public Entry? FindSurface(long id) => _surfaces.FirstOrDefault(s => s.Id == id);
    public FakeNetwork? FindNetwork(long id) => _networks.FirstOrDefault(n => n.Id == id);
    public FakeNetwork? FindNetworkByName(string name)
        => _networks.FirstOrDefault(n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase));

    public long NextPipeId() => _nextPipeId++;
}

/// <summary>Read-only alignment repository over the in-memory drawing.</summary>
internal sealed class FakeAlignmentRepository : IAlignmentRepository
{
    private readonly InMemoryDrawing _drawing;
    public FakeAlignmentRepository(InMemoryDrawing drawing) => _drawing = drawing;

    public AlignmentCollection GetAll()
        => new(_drawing.Alignments.Select(a => AlignmentInfo(a)).ToList());

    public AlignmentInfo GetByName(string name)
        => _drawing.Alignments.FirstOrDefault(a =>
               string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)) is { } found
            ? AlignmentInfo(found)
            : throw new DomainException(DomainErrorCode.EntityNotFound, "not found");

    public AlignmentInfo GetById(long id)
        => _drawing.FindAlignment(id) is { } found
            ? AlignmentInfo(found)
            : throw new DomainException(DomainErrorCode.EntityNotFound, "not found");

    public bool Exists(string name)
        => _drawing.Alignments.Any(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));

    public bool ExistsName(string name, long? exceptId = null)
        => _drawing.Alignments.Any(a =>
            string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)
            && (exceptId is null || a.Id != exceptId));

    public int Count() => _drawing.Alignments.Count;

    public PageResult<AlignmentInfo> Query(QueryRequest request)
        => QueryEngine.Apply(GetAll().Items, request);

    internal static AlignmentInfo AlignmentInfo(InMemoryDrawing.Entry entry) => new()
    {
        Id = entry.Id,
        Name = entry.Name,
        Kind = AlignmentKind.Centerline,
        Length = 1_000,
        StartingStation = 0,
        EndingStation = 1_000,
    };
}

/// <summary>Read-only surface repository over the in-memory drawing.</summary>
internal sealed class FakeSurfaceRepository : ISurfaceRepository
{
    private readonly InMemoryDrawing _drawing;
    public FakeSurfaceRepository(InMemoryDrawing drawing) => _drawing = drawing;

    public SurfaceCollection GetAll()
        => new(_drawing.Surfaces.Select(s => SurfaceInfo(s)).ToList());

    public SurfaceInfo GetByName(string name)
        => _drawing.Surfaces.FirstOrDefault(s =>
               string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) is { } found
            ? SurfaceInfo(found)
            : throw new DomainException(DomainErrorCode.EntityNotFound, "not found");

    public SurfaceInfo GetById(long id)
        => _drawing.FindSurface(id) is { } found
            ? SurfaceInfo(found)
            : throw new DomainException(DomainErrorCode.EntityNotFound, "not found");

    public bool Exists(string name)
        => _drawing.Surfaces.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    public bool ExistsName(string name, long? exceptId = null)
        => _drawing.Surfaces.Any(s =>
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)
            && (exceptId is null || s.Id != exceptId));

    public int Count() => _drawing.Surfaces.Count;

    public PageResult<SurfaceInfo> Query(QueryRequest request)
        => QueryEngine.Apply(GetAll().Items, request);

    internal static SurfaceInfo SurfaceInfo(InMemoryDrawing.Entry entry) => new()
    {
        Id = entry.Id,
        Name = entry.Name,
        Kind = SurfaceKind.Tin,
    };
}

/// <summary>Rename write repository for alignments over the in-memory drawing.</summary>
internal sealed class FakeAlignmentRenameRepository : IAlignmentRenameRepository
{
    private readonly InMemoryDrawing _drawing;
    public FakeAlignmentRenameRepository(InMemoryDrawing drawing) => _drawing = drawing;

    public RenameOutcome Rename(IWriteTransaction transaction, long id, string newName)
    {
        if (transaction.Handle is not InMemoryDrawing drawing)
        {
            throw new DomainException(DomainErrorCode.TransactionFailed, "bad transaction handle");
        }

        InMemoryDrawing.Entry? entry = drawing.FindAlignment(id)
            ?? throw new DomainException(DomainErrorCode.EntityNotFound, "not found");
        string previous = entry.Name;
        entry.Name = newName;
        return new RenameOutcome(id, previous, newName);
    }
}

/// <summary>Rename write repository for surfaces over the in-memory drawing.</summary>
internal sealed class FakeSurfaceRenameRepository : ISurfaceRenameRepository
{
    private readonly InMemoryDrawing _drawing;
    public FakeSurfaceRenameRepository(InMemoryDrawing drawing) => _drawing = drawing;

    public RenameOutcome Rename(IWriteTransaction transaction, long id, string newName)
    {
        if (transaction.Handle is not InMemoryDrawing drawing)
        {
            throw new DomainException(DomainErrorCode.TransactionFailed, "bad transaction handle");
        }

        InMemoryDrawing.Entry? entry = drawing.FindSurface(id)
            ?? throw new DomainException(DomainErrorCode.EntityNotFound, "not found");
        string previous = entry.Name;
        entry.Name = newName;
        return new RenameOutcome(id, previous, newName);
    }
}

/// <summary>Read-only pipe network repository over the in-memory drawing.</summary>
internal sealed class FakePipeRepository : IPipeRepository
{
    private readonly InMemoryDrawing _drawing;
    public FakePipeRepository(InMemoryDrawing drawing) => _drawing = drawing;

    public PipeNetworkCollection GetAll() => new(_drawing.Networks.Select(PipeNetworkInfo).ToList());

    public PipeNetworkInfo GetByName(string name)
        => _drawing.FindNetworkByName(name) is { } found
            ? PipeNetworkInfo(found)
            : throw new DomainException(DomainErrorCode.EntityNotFound, $"No pipe network named '{name}' was found.");

    public PipeNetworkInfo GetById(long id)
        => _drawing.FindNetwork(id) is { } found
            ? PipeNetworkInfo(found)
            : throw new DomainException(DomainErrorCode.EntityNotFound, "not found");

    public bool Exists(string name) => _drawing.FindNetworkByName(name) is not null;

    public int Count() => _drawing.Networks.Count;

    public PageResult<PipeNetworkInfo> Query(QueryRequest request)
        => QueryEngine.Apply(GetAll().Items, request);

    internal static PipeNetworkInfo PipeNetworkInfo(InMemoryDrawing.FakeNetwork network) => new()
    {
        Id = network.Id,
        Name = network.Name,
        PartsListName = "Fake Parts List",
        Pipes = network.Pipes.Select(p => new PipeInfo
        {
            Id = p.Id,
            Name = p.Name,
            NetworkId = network.Id,
        }).ToList(),
    };
}

/// <summary>Create-pipe write repository over the in-memory drawing: resolves the pipe part family
/// by matching (case-insensitive substring) against the network's assigned family descriptions,
/// snaps to the closest available size, and adds a fake pipe — mirroring the real repository's
/// resolution rules without any Autodesk dependency.</summary>
internal sealed class FakePipeCreateRepository : IPipeCreateRepository
{
    private readonly InMemoryDrawing _drawing;
    public FakePipeCreateRepository(InMemoryDrawing drawing) => _drawing = drawing;

    public CreatePipeOutcome Create(IWriteTransaction transaction, long networkId, CreatePipeSpecification specification)
    {
        if (transaction.Handle is not InMemoryDrawing drawing)
        {
            throw new DomainException(DomainErrorCode.TransactionFailed, "bad transaction handle");
        }

        InMemoryDrawing.FakeNetwork network = drawing.FindNetwork(networkId)
            ?? throw new DomainException(DomainErrorCode.EntityNotFound, $"No pipe network with id {networkId} was found.");

        List<InMemoryDrawing.FakePartFamily> matches = network.PartFamilies
            .Where(f => f.Description.Contains(specification.PartFamilyMatch, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            string available = string.Join(", ", network.PartFamilies.Select(f => f.Description));
            throw new DomainException(
                DomainErrorCode.PartNotFound,
                $"No pipe part family in network '{network.Name}' matches '{specification.PartFamilyMatch}'. " +
                $"Available families: {(available.Length == 0 ? "(none)" : available)}.");
        }

        if (matches.Count > 1)
        {
            throw new DomainException(
                DomainErrorCode.PartNotFound,
                $"'{specification.PartFamilyMatch}' matches more than one pipe part family in network " +
                $"'{network.Name}': {string.Join(", ", matches.Select(m => m.Description))}. Use a more specific match.");
        }

        InMemoryDrawing.FakePartFamily family = matches[0];
        if (family.Sizes.Count == 0)
        {
            throw new DomainException(DomainErrorCode.PartNotFound, $"Part family '{family.Description}' has no sizes defined.");
        }

        InMemoryDrawing.FakePartSize size = family.Sizes
            .OrderBy(s => Math.Abs(s.DiameterMm - specification.DiameterMm))
            .First();

        double length3D = Math.Sqrt(
            Math.Pow(specification.EndEasting - specification.StartEasting, 2) +
            Math.Pow(specification.EndNorthing - specification.StartNorthing, 2) +
            Math.Pow(specification.EndElevation - specification.StartElevation, 2));

        long pipeId = drawing.NextPipeId();
        var pipe = new InMemoryDrawing.FakePipe(pipeId, $"Pipe-{pipeId}", family.Description, size)
        {
            StartEasting = specification.StartEasting,
            StartNorthing = specification.StartNorthing,
            StartElevation = specification.StartElevation,
            EndEasting = specification.EndEasting,
            EndNorthing = specification.EndNorthing,
            EndElevation = specification.EndElevation,
            Length3D = length3D,
            Description = specification.Description,
        };
        network.Pipes.Add(pipe);

        return new CreatePipeOutcome
        {
            PipeId = pipe.Id,
            Name = pipe.Name,
            NetworkId = network.Id,
            NetworkName = network.Name,
            PartFamilyName = family.Description,
            PartSizeName = size.Name,
            Material = null,
            InnerDiameterOrWidth = size.DiameterMm / 1000.0,
            OuterDiameterOrWidth = size.DiameterMm / 1000.0,
            StartEasting = pipe.StartEasting,
            StartNorthing = pipe.StartNorthing,
            StartElevation = pipe.StartElevation,
            EndEasting = pipe.EndEasting,
            EndNorthing = pipe.EndNorthing,
            EndElevation = pipe.EndElevation,
            Length3D = pipe.Length3D,
        };
    }
}
