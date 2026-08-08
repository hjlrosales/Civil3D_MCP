using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Civil3D.Domain.Commands.Transactions;

/// <summary>
/// Default <see cref="ITransactionPipeline"/>. Single-writer: exactly one transaction is active
/// at a time (Autodesk is single-threaded); a nested <see cref="Execute"/> throws
/// <c>TransactionAlreadyActive</c>. Read-only runs invoke the work with a null transaction and
/// never touch the provider. A timeout or cancellation mid-transaction rolls back and surfaces a
/// <see cref="CommandException"/> with <c>TransactionTimeout</c>/<c>Cancelled</c>; any other
/// failure rolls back and rethrows. The transaction is always disposed.
/// </summary>
public sealed class TransactionPipeline : ITransactionPipeline
{
    private readonly ITransactionProvider _provider;
    private readonly IDomainEventDispatcher _events;
    private readonly ILogger<TransactionPipeline> _logger;
    private readonly object _sync = new();
    private IWriteTransaction? _active;

    /// <summary>Creates the pipeline.</summary>
    /// <param name="provider">The write transaction provider (Autodesk-backed in the bridge).</param>
    /// <param name="events">Domain event dispatcher.</param>
    /// <param name="logger">Logger.</param>
    public TransactionPipeline(
        ITransactionProvider provider,
        IDomainEventDispatcher events,
        ILogger<TransactionPipeline> logger)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public TResult Execute<TResult>(Func<IWriteTransaction?, CancellationToken, TResult> work, TransactionOptions options)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(options);

        if (options.ReadOnly)
        {
            // Read-only detection: no begin/commit/rollback; the work sees no transaction but the
            // caller's effective token (so it can still observe cancellation).
            return work(null, options.CancellationToken);
        }

        lock (_sync)
        {
            if (_active is not null)
            {
                throw new CommandException(
                    CommandErrorCode.TransactionAlreadyActive,
                    $"A transaction is already active for '{_active}'; nested transactions are not supported.");
            }
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken);
        if (options.Timeout is { } timeout)
        {
            cts.CancelAfter(timeout);
        }

        IWriteTransaction? transaction = null;
        var timer = Stopwatch.StartNew();
        try
        {
            transaction = _provider.Begin(options.CommandName, cts.Token);
            lock (_sync)
            {
                _active = transaction;
            }

            TResult result = work(transaction, cts.Token);

            transaction.Commit();
            timer.Stop();
            _logger.LogInformation(
                "Transaction for command {Command} committed in {DurationMs} ms (correlation {CorrelationId}).",
                options.CommandName, timer.ElapsedMilliseconds, options.CorrelationId);
            _events.PublishAsync(
                new TransactionCommitted(options.CommandName, options.CorrelationId, timer.Elapsed),
                cts.Token).GetAwaiter().GetResult();
            return result;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !options.CancellationToken.IsCancellationRequested)
        {
            timer.Stop();
            return FailAndThrow<TResult>(options, transaction, timer, "timeout",
                new CommandException(
                    CommandErrorCode.TransactionTimeout,
                    $"The transaction for command '{options.CommandName}' timed out after {options.Timeout}."));
        }
        catch (OperationCanceledException)
        {
            timer.Stop();
            return FailAndThrow<TResult>(options, transaction, timer, "cancelled",
                new CommandException(
                    CommandErrorCode.Cancelled,
                    $"The transaction for command '{options.CommandName}' was cancelled."));
        }
        catch (Exception ex)
        {
            timer.Stop();
            string reason = $"exception: {ex.GetType().Name}";
            Rollback(options, transaction, timer, reason);
            _logger.LogWarning(
                ex,
                "Transaction for command {Command} rolled back after {DurationMs} ms ({Reason}; correlation {CorrelationId}).",
                options.CommandName, timer.ElapsedMilliseconds, reason, options.CorrelationId);
            throw; // Preserve the original failure (CommandException, DomainException, …) for the tool layer.
        }
        finally
        {
            transaction?.Dispose();
            lock (_sync)
            {
                _active = null;
            }
        }
    }

    private TResult FailAndThrow<TResult>(
        TransactionOptions options,
        IWriteTransaction? transaction,
        Stopwatch timer,
        string reason,
        CommandException exception)
    {
        Rollback(options, transaction, timer, reason);
        _logger.LogWarning(
            "Transaction for command {Command} rolled back after {DurationMs} ms ({Reason}; correlation {CorrelationId}).",
            options.CommandName, timer.ElapsedMilliseconds, reason, options.CorrelationId);
        throw exception;
    }

    private void Rollback(TransactionOptions options, IWriteTransaction? transaction, Stopwatch timer, string reason)
    {
        if (transaction is null)
        {
            return;
        }

        try
        {
            transaction.Rollback();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rollback failed for command {Command} (correlation {CorrelationId}).",
                options.CommandName, options.CorrelationId);
        }
        finally
        {
            _events.PublishAsync(
                new TransactionRolledBack(options.CommandName, options.CorrelationId, reason, timer.Elapsed),
                CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
