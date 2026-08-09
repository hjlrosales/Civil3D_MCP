using Civil3D.Domain.Commands;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Errors;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Pipes.Repositories;

namespace Civil3D.Domain.Pipes.Services;

/// <summary>
/// Create-pipe-network orchestration: confirms the name is free (via the read repository, so a
/// duplicate fails with the standard <c>DuplicateName</c> before any Autodesk write is attempted),
/// invokes the write repository inside the active transaction, and raises the <c>NetworkCreated</c>
/// domain event. Autodesk-free.
/// </summary>
public sealed class CreatePipeNetworkService : ICreatePipeNetworkService
{
    private readonly IPipeRepository _read;
    private readonly IPipeNetworkCreateRepository _write;
    private readonly IDomainEventDispatcher _events;

    /// <summary>Creates the service.</summary>
    /// <param name="read">The read-only pipe network repository.</param>
    /// <param name="write">The pipe network create write repository.</param>
    /// <param name="events">The domain event dispatcher.</param>
    public CreatePipeNetworkService(IPipeRepository read, IPipeNetworkCreateRepository write, IDomainEventDispatcher events)
    {
        _read = read ?? throw new ArgumentNullException(nameof(read));
        _write = write ?? throw new ArgumentNullException(nameof(write));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    /// <inheritdoc />
    public CreatePipeNetworkResult Create(IWriteTransaction transaction, CreatePipeNetworkSpecification specification, ICommandExecutionContext context)
    {
        // 1. The network name must be free; create_pipe_network never overwrites an existing network.
        if (_read.Exists(specification.Name))
        {
            throw new DomainException(
                DomainErrorCode.DuplicateName,
                $"A pipe network named '{specification.Name}' already exists in the drawing.");
        }

        // 2. Perform the Autodesk creation inside the active write transaction.
        CreatePipeNetworkOutcome outcome = _write.Create(transaction, specification);

        // 3. Raise the domain event.
        _events.PublishAsync(
            new NetworkCreated(
                NetworkName: outcome.Name,
                NetworkId: outcome.NetworkId,
                PartsListName: outcome.PartsListName,
                CorrelationId: context.CorrelationId,
                SessionId: context.SessionId),
            context.CancellationToken).GetAwaiter().GetResult();

        return new CreatePipeNetworkResult
        {
            NetworkId = outcome.NetworkId,
            Name = outcome.Name,
            Description = specification.Description,
            PartsListName = outcome.PartsListName,
            FamiliesAdded = outcome.FamiliesAdded,
            FamiliesFailed = outcome.FamiliesFailed,
            Success = true,
            TimestampUtc = DateTime.UtcNow,
        };
    }
}
