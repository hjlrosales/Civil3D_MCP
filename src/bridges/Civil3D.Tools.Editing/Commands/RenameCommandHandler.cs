using Civil3D.Domain.Commands;
using Civil3D.Domain.Commands.Transactions;

namespace Civil3D.Tools.Editing.Commands;

/// <summary>
/// Generic handler for the rename commands. Orchestrates the write transaction lifecycle for the
/// rename: opens an undo unit, delegates to the discipline rename service (which validates and
/// performs the Autodesk rename inside the active write transaction), and commits the undo unit
/// on success or rolls it back on failure. Autodesk-free.
/// </summary>
/// <typeparam name="TCommand">The rename command (alignment or surface).</typeparam>
public sealed class RenameCommandHandler<TCommand> : ICommandHandler<TCommand, RenameResult>
    where TCommand : RenameCommandBase
{
    private readonly Func<IWriteTransaction, long, string, ICommandExecutionContext, RenameResult> _rename;

    /// <summary>Creates the handler bound to a discipline rename service.</summary>
    /// <param name="rename">The discipline rename delegate (service call).</param>
    public RenameCommandHandler(Func<IWriteTransaction, long, string, ICommandExecutionContext, RenameResult> rename)
    {
        _rename = rename ?? throw new ArgumentNullException(nameof(rename));
    }

    /// <inheritdoc />
    public RenameResult Handle(
        TCommand command,
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

        using IUndoUnit undo = context.Undo.Begin($"Rename {command.ObjectId}");
        try
        {
            RenameResult result = _rename(transaction, command.ObjectId, command.NewName, context);
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
