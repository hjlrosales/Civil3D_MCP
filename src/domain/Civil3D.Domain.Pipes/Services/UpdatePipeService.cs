using Civil3D.Domain.Commands;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Pipes.Repositories;

namespace Civil3D.Domain.Pipes.Services;

/// <summary>
/// Update-pipe orchestration: invokes the write repository inside the active transaction (the
/// repository resolves the pipe by id and throws <c>EntityNotFound</c> when it is missing, before
/// any change is applied) and raises the <c>PartUpdated</c> domain event. Autodesk-free.
/// </summary>
public sealed class UpdatePipeService : IUpdatePipeService
{
    private readonly IPipeUpdateRepository _write;
    private readonly IDomainEventDispatcher _events;

    /// <summary>Creates the service.</summary>
    /// <param name="write">The pipe update write repository.</param>
    /// <param name="events">The domain event dispatcher.</param>
    public UpdatePipeService(IPipeUpdateRepository write, IDomainEventDispatcher events)
    {
        _write = write ?? throw new ArgumentNullException(nameof(write));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    /// <inheritdoc />
    public UpdatePipeResult Update(IWriteTransaction transaction, UpdatePipeSpecification specification, ICommandExecutionContext context)
    {
        // Perform the Autodesk update inside the active write transaction.
        UpdatePipeOutcome outcome = _write.Update(transaction, specification);

        // Raise the domain event.
        _events.PublishAsync(
            new PartUpdated(
                PartType: "pipe",
                PartId: outcome.PipeId,
                NetworkId: outcome.NetworkId,
                Name: outcome.Name,
                CorrelationId: context.CorrelationId,
                SessionId: context.SessionId),
            context.CancellationToken).GetAwaiter().GetResult();

        return new UpdatePipeResult
        {
            PipeId = outcome.PipeId,
            Name = outcome.Name,
            NetworkId = outcome.NetworkId,
            NetworkName = outcome.NetworkName,
            PartFamilyName = outcome.PartFamilyName,
            PartSizeName = outcome.PartSizeName,
            Material = outcome.Material,
            InnerDiameterOrWidth = outcome.InnerDiameterOrWidth,
            OuterDiameterOrWidth = outcome.OuterDiameterOrWidth,
            StartEasting = outcome.StartEasting,
            StartNorthing = outcome.StartNorthing,
            StartElevation = outcome.StartElevation,
            EndEasting = outcome.EndEasting,
            EndNorthing = outcome.EndNorthing,
            EndElevation = outcome.EndElevation,
            Length3D = outcome.Length3D,
            ChangesApplied = outcome.ChangesApplied,
            Success = true,
            TimestampUtc = DateTime.UtcNow,
        };
    }
}
