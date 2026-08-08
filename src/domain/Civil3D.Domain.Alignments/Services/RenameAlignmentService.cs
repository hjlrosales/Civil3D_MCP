using Civil3D.Domain.Alignments.Data;
using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Alignments.Repositories;
using Civil3D.Domain.Commands;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Dtos;
using Civil3D.Domain.Errors;

namespace Civil3D.Domain.Alignments.Services;

/// <summary>
/// Rename orchestration for alignments: reads the current name, rejects no-op renames and
/// duplicate names, invokes the write repository inside the active transaction, and raises the
/// <c>ObjectRenamed</c> domain event. Autodesk-free.
/// </summary>
public sealed class RenameAlignmentService : IRenameAlignmentService
{
    private readonly IAlignmentRepository _read;
    private readonly IAlignmentRenameRepository _write;
    private readonly IDomainEventDispatcher _events;

    /// <summary>Creates the service.</summary>
    /// <param name="read">The read-only alignment repository.</param>
    /// <param name="write">The alignment rename write repository.</param>
    /// <param name="events">The domain event dispatcher.</param>
    public RenameAlignmentService(
        IAlignmentRepository read,
        IAlignmentRenameRepository write,
        IDomainEventDispatcher events)
    {
        _read = read ?? throw new ArgumentNullException(nameof(read));
        _write = write ?? throw new ArgumentNullException(nameof(write));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    /// <inheritdoc />
    public RenameResult Rename(IWriteTransaction transaction, long id, string newName, ICommandExecutionContext context)
    {
        // 1. The object must exist and its current name must be readable.
        AlignmentInfo current = _read.GetById(id);

        // 2. No-op renames are rejected before any Autodesk write.
        if (string.Equals(current.Name, newName, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(
                DomainErrorCode.InvalidName,
                $"The alignment is already named '{current.Name}'.");
        }

        // 3. The new name must be unique (excluding the object being renamed).
        if (_read.ExistsName(newName, exceptId: id))
        {
            throw new DomainException(
                DomainErrorCode.DuplicateName,
                $"An alignment named '{newName}' already exists.");
        }

        // 4. Perform the Autodesk rename inside the active write transaction.
        RenameOutcome outcome = _write.Rename(transaction, id, newName);

        // 5. Raise the domain event.
        _events.PublishAsync(
            new ObjectRenamed(
                ObjectType: "alignment",
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
