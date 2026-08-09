using Civil3D.Domain.Commands;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Pipes.Services;

namespace Civil3D.Tools.Editing.Commands;

/// <summary>
/// Handler for <see cref="CreatePipeNetworkCommand"/>. Orchestrates the write transaction
/// lifecycle: opens an undo unit, delegates to <see cref="ICreatePipeNetworkService"/> (which
/// validates the name and performs the Autodesk creation inside the active write transaction),
/// and commits the undo unit on success or rolls it back on failure. Autodesk-free.
/// </summary>
public sealed class CreatePipeNetworkCommandHandler : ICommandHandler<CreatePipeNetworkCommand, CreatePipeNetworkResult>
{
    private readonly ICreatePipeNetworkService _service;

    /// <summary>Creates the handler bound to the create-pipe-network domain service.</summary>
    /// <param name="service">The create-pipe-network domain service.</param>
    public CreatePipeNetworkCommandHandler(ICreatePipeNetworkService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <inheritdoc />
    public CreatePipeNetworkResult Handle(
        CreatePipeNetworkCommand command,
        ICommandExecutionContext context,
        IWriteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is null)
        {
            throw new CommandException(
                CommandErrorCode.TransactionFailed,
                $"Command '{command.Name}' requires a write transaction.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        using IUndoUnit undo = context.Undo.Begin($"Create pipe network {command.NetworkName}");
        try
        {
            var specification = new CreatePipeNetworkSpecification
            {
                Name = command.NetworkName,
                Description = command.Description,
                PartsListName = command.PartsListName,
                Materials = command.Materials,
                SizesMm = command.SizesMm,
            };

            CreatePipeNetworkResult result = _service.Create(transaction, specification, context);
            undo.Commit();
            return result;
        }
        catch
        {
            undo.Rollback();
            throw;
        }
    }
}
