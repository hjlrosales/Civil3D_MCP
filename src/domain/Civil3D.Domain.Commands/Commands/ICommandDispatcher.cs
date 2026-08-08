namespace Civil3D.Domain.Commands;

/// <summary>
/// Executes a command through the full pipeline: validation, permission check, confirmation
/// check, progress reporting, the write transaction (commit/rollback), domain events and logging.
/// Handlers and validators are resolved through dependency injection. Throws
/// <see cref="CommandException"/> on failure; the tool layer maps codes to protocol errors.
/// </summary>
public interface ICommandDispatcher
{
    /// <summary>Dispatches a command for execution.</summary>
    /// <typeparam name="TCommand">The command type (registered handler required).</typeparam>
    /// <typeparam name="TResult">The command result type.</typeparam>
    /// <param name="command">The command instance.</param>
    /// <param name="context">Per-command execution context.</param>
    /// <param name="cancellationToken">Effective cancellation token.</param>
    Task<TResult> DispatchAsync<TCommand, TResult>(
        TCommand command,
        ICommandExecutionContext context,
        CancellationToken cancellationToken = default)
        where TCommand : class, ICommand<TResult>;
}
