using Civil3D.Domain.Commands;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Pipes.Repositories;

namespace Civil3D.Domain.Pipes.Services;

/// <summary>
/// Create-pipe orchestration for pipe networks: confirms the target network exists (via the
/// read repository, so a missing network fails with the standard <c>EntityNotFound</c> before any
/// Autodesk write is attempted), invokes the write repository inside the active transaction, and
/// raises the <c>PartCreated</c> domain event. Autodesk-free.
/// </summary>
public sealed class CreatePipeService : ICreatePipeService
{
    private readonly IPipeRepository _read;
    private readonly IPipeCreateRepository _write;
    private readonly IDomainEventDispatcher _events;

    /// <summary>Creates the service.</summary>
    /// <param name="read">The read-only pipe network repository.</param>
    /// <param name="write">The pipe create write repository.</param>
    /// <param name="events">The domain event dispatcher.</param>
    public CreatePipeService(IPipeRepository read, IPipeCreateRepository write, IDomainEventDispatcher events)
    {
        _read = read ?? throw new ArgumentNullException(nameof(read));
        _write = write ?? throw new ArgumentNullException(nameof(write));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    /// <inheritdoc />
    public CreatePipeResult Create(IWriteTransaction transaction, CreatePipeSpecification specification, ICommandExecutionContext context)
    {
        // 1. The target network must already exist; create_pipe never creates one implicitly.
        PipeNetworkInfo network = _read.GetByName(specification.NetworkName);

        // 2. Perform the Autodesk creation inside the active write transaction.
        CreatePipeOutcome outcome = _write.Create(transaction, network.Id, specification);

        // 3. Raise the domain event.
        _events.PublishAsync(
            new PartCreated(
                PartType: "pipe",
                PartId: outcome.PipeId,
                NetworkId: network.Id,
                Name: outcome.Name,
                CorrelationId: context.CorrelationId,
                SessionId: context.SessionId),
            context.CancellationToken).GetAwaiter().GetResult();

        return new CreatePipeResult
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
            Success = true,
            TimestampUtc = DateTime.UtcNow,
        };
    }
}
