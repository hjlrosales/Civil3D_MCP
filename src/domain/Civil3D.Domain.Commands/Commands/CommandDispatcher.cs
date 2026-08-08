using System.Diagnostics;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Errors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Civil3D.Domain.Commands;

/// <summary>
/// Default <see cref="ICommandDispatcher"/>. Runs each command through the fixed pipeline:
/// <list type="number">
/// <item>publishes <c>CommandStarted</c>;</item>
/// <item>runs every registered <see cref="ICommandValidator{TCommand}"/> (failures → <c>E_VALIDATION_FAILED</c>);</item>
/// <item>checks the granted permission against <see cref="ICommand.RequiredPermission"/> (→ <c>E_PERMISSION_DENIED</c>);</item>
/// <item>checks confirmation for commands that require it (→ <c>E_CONFIRMATION_REQUIRED</c>);</item>
/// <item>executes the handler through <see cref="ITransactionPipeline"/> (commit on success, rollback otherwise);</item>
/// <item>publishes <c>CommandCompleted</c>/<c>CommandFailed</c> and logs name, validation result,
/// execution time, transaction outcome, correlation and session ids.</item>
/// </list>
/// Handlers and validators come from the container by closed generic type, so commands register
/// freely without any per-command switch.
/// </summary>
public sealed class CommandDispatcher : ICommandDispatcher
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private readonly IServiceProvider _services;
    private readonly ITransactionPipeline _transactions;
    private readonly IDomainEventDispatcher _events;
    private readonly ILogger<CommandDispatcher> _logger;

    /// <summary>Creates the dispatcher.</summary>
    /// <param name="services">Container used to resolve handlers and validators by closed generic type.</param>
    /// <param name="transactions">The write transaction pipeline.</param>
    /// <param name="events">Domain event dispatcher.</param>
    /// <param name="logger">Logger.</param>
    public CommandDispatcher(
        IServiceProvider services,
        ITransactionPipeline transactions,
        IDomainEventDispatcher events,
        ILogger<CommandDispatcher> logger)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<TResult> DispatchAsync<TCommand, TResult>(
        TCommand command,
        ICommandExecutionContext context,
        CancellationToken cancellationToken = default)
        where TCommand : class, ICommand<TResult>
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);

        var timer = Stopwatch.StartNew();
        string correlationId = context.CorrelationId;
        await _events.PublishAsync(new CommandStarted(command.Name, correlationId, context.SessionId), cancellationToken);

        try
        {
            // 1. Validation: all registered validators, aggregated; nothing else runs on failure.
            var failures = Validate(command);
            if (failures.Count > 0)
            {
                string joined = string.Join("; ", failures.Select(f => $"{f.Field}: {f.Message}"));
                _logger.LogWarning(
                    "Command {Command} failed validation: {Errors} (correlation {CorrelationId}, session {SessionId}).",
                    command.Name, joined, correlationId, context.SessionId);
                throw new CommandException(
                    CommandErrorCode.ValidationFailed,
                    $"Validation failed for command '{command.Name}': {joined}");
            }

            context.Progress.Report(20, "Validated");

            // 2. Permission check (enum order = escalation).
            if (command.RequiredPermission > context.EffectivePermission)
            {
                _logger.LogWarning(
                    "Command {Command} requires permission {Required} but only {Granted} was granted (correlation {CorrelationId}).",
                    command.Name, command.RequiredPermission, context.EffectivePermission, correlationId);
                throw new CommandException(
                    CommandErrorCode.PermissionDenied,
                    $"Command '{command.Name}' requires {command.RequiredPermission} permission.");
            }

            // 3. Confirmation check (dangerous commands only).
            if (command.RequiresConfirmation && !context.ConfirmationGranted)
            {
                _logger.LogWarning(
                    "Command {Command} requires confirmation that was not granted (correlation {CorrelationId}).",
                    command.Name, correlationId);
                throw new CommandException(
                    CommandErrorCode.ConfirmationRequired,
                    $"Command '{command.Name}' requires explicit confirmation before it can run.");
            }

            context.Progress.Report(40, "Checked");

            // 4. Execute the handler inside the transaction pipeline.
            ICommandHandler<TCommand, TResult> handler = ResolveHandler<TCommand, TResult>();
            TResult result = await ExecuteHandlerAsync(command, handler, context, cancellationToken);

            timer.Stop();
            context.Progress.Report(100, "Complete");
            _logger.LogInformation(
                "Command {Command} completed in {ExecutionTime} ms (correlation {CorrelationId}, session {SessionId}).",
                command.Name, timer.ElapsedMilliseconds, correlationId, context.SessionId);
            await _events.PublishAsync(
                new CommandCompleted(command.Name, correlationId, context.SessionId, timer.ElapsedMilliseconds, Committed: !command.IsReadOnly),
                cancellationToken);
            return result;
        }
        catch (CommandException ex)
        {
            timer.Stop();
            await _events.PublishAsync(
                new CommandFailed(command.Name, correlationId, context.SessionId, ex.Code.ToString(), RollbackReason: null),
                CancellationToken.None);
            _logger.LogWarning(
                "Command {Command} failed with {ErrorCode}: {Message} (correlation {CorrelationId}).",
                command.Name, ex.Code, ex.Message, correlationId);
            throw;
        }
        catch (DomainException ex)
        {
            // Domain failures (no document, transaction failed, entity not found, …) pass through
            // unchanged so the tool layer maps the stable code to the matching protocol error.
            timer.Stop();
            await _events.PublishAsync(
                new CommandFailed(command.Name, correlationId, context.SessionId, ex.Code.ToString(), RollbackReason: null),
                CancellationToken.None);
            _logger.LogWarning(
                "Command {Command} failed with domain code {ErrorCode}: {Message} (correlation {CorrelationId}).",
                command.Name, ex.Code, ex.Message, correlationId);
            throw;
        }
        catch (OperationCanceledException)
        {
            timer.Stop();
            await _events.PublishAsync(
                new CommandFailed(command.Name, correlationId, context.SessionId, "Cancelled", RollbackReason: null),
                CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            timer.Stop();
            await _events.PublishAsync(
                new CommandFailed(command.Name, correlationId, context.SessionId, "Internal", RollbackReason: null),
                CancellationToken.None);
            _logger.LogError(ex, "Command {Command} failed (correlation {CorrelationId}).", command.Name, correlationId);
            throw new CommandException(
                CommandErrorCode.Internal,
                "An unexpected error occurred while executing the command.",
                ex);
        }
    }

    private async Task<TResult> ExecuteHandlerAsync<TCommand, TResult>(
        TCommand command,
        ICommandHandler<TCommand, TResult> handler,
        ICommandExecutionContext context,
        CancellationToken cancellationToken)
        where TCommand : class, ICommand<TResult>
    {
        context.Progress.Report(60, command.IsReadOnly ? "Executing (read-only)" : "Executing");
        var options = new TransactionOptions
        {
            CommandName = command.Name,
            CorrelationId = context.CorrelationId,
            ReadOnly = command.IsReadOnly,
            Timeout = command.IsReadOnly ? null : DefaultTimeout,
            CancellationToken = cancellationToken,
        };

        // The pipeline runs synchronously on the calling (application-context) thread; the async
        // wrapper preserves the awaited surface without hopping threads. The pipeline hands the
        // handler its timeout/cancellation-linked token so long work observes both.
        return await Task.FromResult(
            _transactions.Execute(
                (transaction, token) => handler.Handle(command, context, transaction, token),
                options));
    }

    private IReadOnlyList<ValidationFailure> Validate<TCommand>(TCommand command)
        where TCommand : ICommand
    {
        Type validatorType = typeof(ICommandValidator<>).MakeGenericType(typeof(TCommand));
        var validators = (IEnumerable<object>)_services.GetServices(validatorType);

        var failures = new List<ValidationFailure>();
        foreach (object validator in validators)
        {
            var result = ((ICommandValidator<TCommand>)validator).Validate(command);
            if (!result.IsValid)
            {
                failures.AddRange(result.Failures);
            }
        }

        return failures;
    }

    private ICommandHandler<TCommand, TResult> ResolveHandler<TCommand, TResult>()
        where TCommand : class, ICommand<TResult>
    {
        Type handlerType = typeof(ICommandHandler<,>).MakeGenericType(typeof(TCommand), typeof(TResult));
        try
        {
            return (ICommandHandler<TCommand, TResult>)_services.GetRequiredService(handlerType);
        }
        catch (InvalidOperationException ex)
        {
            throw new CommandException(
                CommandErrorCode.Internal,
                "No handler is registered for command '" + typeof(TCommand).Name + "'.",
                ex);
        }
    }
}
