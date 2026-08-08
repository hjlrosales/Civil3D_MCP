using Civil3D.Domain.Commands.Transactions;

namespace Civil3D.Domain.Commands;

/// <summary>
/// Executes a command against the drawing through repositories and services. Handlers never open
/// transactions themselves: for a writing command the dispatcher passes the active
/// <see cref="IWriteTransaction"/> (already begun and document-locked) and commits it after the
/// handler returns; for a read-only command <c>transaction</c> is null. Handlers
/// should throw <see cref="CommandException"/> or <see cref="Civil3D.Domain.Errors.DomainException"/>;
/// the pipeline rolls back on any failure.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
/// <typeparam name="TResult">The command result type.</typeparam>
public interface ICommandHandler<TCommand, TResult>
    where TCommand : class, ICommand<TResult>
{
    /// <summary>Executes the command.</summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="context">Per-command execution context (ids, progress, undo).</param>
    /// <param name="transaction">The active write transaction for writing commands, or null for read-only commands.</param>
    /// <param name="cancellationToken">Effective cancellation token.</param>
    TResult Handle(TCommand command, ICommandExecutionContext context, IWriteTransaction? transaction, CancellationToken cancellationToken);
}
