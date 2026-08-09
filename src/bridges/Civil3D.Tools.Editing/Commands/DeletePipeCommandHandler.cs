using Civil3D.Domain.Commands;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Pipes.Services;

namespace Civil3D.Tools.Editing.Commands;

/// <summary>
/// Handler for <see cref="DeletePipeCommand"/>. Orchestrates the write transaction lifecycle:
/// opens an undo unit, delegates to <see cref="IDeletePipeService"/> (which performs the
/// Autodesk deletion inside the active write transaction), and commits the undo unit on success
/// or rolls it back on failure. Autodesk-free.
/// </summary>
public sealed class DeletePipeCommandHandler : ICommandHandler<DeletePipeCommand, DeletePipeResult>
{
    private readonly IDeletePipeService _service;

    /// <summary>Creates the handler bound to the delete-pipe domain service.</summary>
    /// <param name="service">The delete-pipe domain service.</param>
    public DeletePipeCommandHandler(IDeletePipeService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <inheritdoc />
    public DeletePipeResult Handle(
        DeletePipeCommand command,
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

        using IUndoUnit undo = context.Undo.Begin($"Delete pipe {command.PipeId}");
        try
        {
            var specification = new DeletePipeSpecification
            {
                PipeId = command.PipeId,
            };

            DeletePipeResult result = _service.Delete(transaction, specification, context);
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
