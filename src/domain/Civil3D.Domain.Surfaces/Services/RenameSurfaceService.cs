using Civil3D.Domain.Commands;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Dtos;
using Civil3D.Domain.Errors;
using Civil3D.Domain.Surfaces.Data;
using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Domain.Surfaces.Repositories;

namespace Civil3D.Domain.Surfaces.Services;

/// <summary>
/// Rename orchestration for surfaces: reads the current name, rejects no-op renames and
/// duplicate names, invokes the write repository inside the active transaction, and raises the
/// <c>ObjectRenamed</c> domain event. Autodesk-free.
/// </summary>
public sealed class RenameSurfaceService : IRenameSurfaceService
{
    private readonly ISurfaceRepository _read;
    private readonly ISurfaceRenameRepository _write;
    private readonly IDomainEventDispatcher _events;

    /// <summary>Creates the service.</summary>
    /// <param name="read">The read-only surface repository.</param>
    /// <param name="write">The surface rename write repository.</param>
    /// <param name="events">The domain event dispatcher.</param>
    public RenameSurfaceService(
        ISurfaceRepository read,
        ISurfaceRenameRepository write,
        IDomainEventDispatcher events)
    {
        _read = read ?? throw new ArgumentNullException(nameof(read));
        _write = write ?? throw new ArgumentNullException(nameof(write));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    /// <inheritdoc />
    public RenameResult Rename(IWriteTransaction transaction, long id, string newName, ICommandExecutionContext context)
    {
        SurfaceInfo current = _read.GetById(id);

        if (string.Equals(current.Name, newName, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(
                DomainErrorCode.InvalidName,
                $"The surface is already named '{current.Name}'.");
        }

        if (_read.ExistsName(newName, exceptId: id))
        {
            throw new DomainException(
                DomainErrorCode.DuplicateName,
                $"A surface named '{newName}' already exists.");
        }

        RenameOutcome outcome = _write.Rename(transaction, id, newName);

        _events.PublishAsync(
            new ObjectRenamed(
                ObjectType: "surface",
                ObjectId: id,
                PreviousName: outcome.PreviousName,
                NewName: newName,
                CorrelationId: context.CorrelationId,
                SessionId: context.SessionId),
            context.CancellationToken).GetAwaiter().GetResult();

        return new RenameResult
        {
            ObjectId = id,
            PreviousName = outcome.PreviousName,
            CurrentName = newName,
            Success = true,
            TimestampUtc = DateTime.UtcNow,
        };
    }
}
