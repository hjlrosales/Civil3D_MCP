using Civil3D.Domain.Commands;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Pipes.Repositories;

namespace Civil3D.Domain.Pipes.Services;

/// <summary>
/// Delete-pipe orchestration: invokes the write repository inside the active transaction (the
/// repository resolves the pipe by id and throws <c>EntityNotFound</c> when it is missing,
/// before anything is erased) and raises the <c>PartDeleted</c> domain event. Autodesk-free.
/// </summary>
public sealed class DeletePipeService : IDeletePipeService
{
    private readonly IPipeDeleteRepository _write;
    private readonly IDomainEventDispatcher _events;

    /// <summary>Creates the service.</summary>
    /// <param name="write">The pipe delete write repository.</param>
    /// <param name="events">The domain event dispatcher.</param>
    public DeletePipeService(IPipeDeleteRepository write, IDomainEventDispatcher events)
    {
        _write = write ?? throw new ArgumentNullException(nameof(write));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    /// <inheritdoc />
    public DeletePipeResult Delete(IWriteTransaction transaction, DeletePipeSpecification specification, ICommandExecutionContext context)
    {
        // Perform the Autodesk deletion inside the active write transaction.
        DeletePipeOutcome outcome = _write.Delete(transaction, specification);

        // Raise the domain event.
        _events.PublishAsync(
            new PartDeleted(
                PartType: "pipe",
                PartId: outcome.PipeId,
                NetworkId: outcome.NetworkId,
                Name: outcome.Name,
                CorrelationId: context.CorrelationId,
                SessionId: context.SessionId),
            context.CancellationToken).GetAwaiter().GetResult();

        return new DeletePipeResult
        {
            PipeId = outcome.PipeId,
            Name = outcome.Name,
            NetworkId = outcome.NetworkId,
            NetworkName = outcome.NetworkName,
            PartFamilyName = outcome.PartFamilyName,
            PartSizeName = outcome.PartSizeName,
            Success = true,
            DeletedAtUtc = DateTime.UtcNow,
        };
    }
}
