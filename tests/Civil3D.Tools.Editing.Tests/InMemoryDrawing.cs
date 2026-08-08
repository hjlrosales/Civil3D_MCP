using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Commands;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Dtos;
using Civil3D.Domain.Errors;
using Civil3D.Domain.Query;
using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Domain.Alignments.Repositories;
using Civil3D.Domain.Surfaces.Repositories;

namespace Civil3D.Tools.Editing.Tests;

/// <summary>
/// An in-memory Civil 3D drawing for the rename tests: alignment and surface name tables plus a
/// write transaction whose <see cref="IWriteTransaction.Handle"/> is a typed handle the rename
/// repositories can open for write. Stands in for the Autodesk database/transaction.
/// </summary>
internal sealed class InMemoryDrawing
{
    internal sealed class Entry(long id, string name)
    {
        public long Id { get; } = id;
        public string Name { get; set; } = name;
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

    public IReadOnlyList<Entry> Alignments => _alignments;
    public IReadOnlyList<Entry> Surfaces => _surfaces;

    public InMemoryDrawing(
        IEnumerable<(long Id, string Name)>? alignments = null,
        IEnumerable<(long Id, string Name)>? surfaces = null)
    {
        _alignments = (alignments ?? []).Select(a => new Entry(a.Id, a.Name)).ToList();
        _surfaces = (surfaces ?? []).Select(s => new Entry(s.Id, s.Name)).ToList();
    }

    public Entry? FindAlignment(long id) => _alignments.FirstOrDefault(a => a.Id == id);
    public Entry? FindSurface(long id) => _surfaces.FirstOrDefault(s => s.Id == id);
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
