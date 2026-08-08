namespace Civil3D.Domain.Commands.Transactions;

/// <summary>
/// Runs command work inside a single write transaction with commit/rollback semantics. Owns the
/// transaction lifecycle: begin, execute, commit on success, rollback on any failure, dispose in
/// all cases. Detects nested transactions, honours read-only commands, applies timeouts and
/// cancellation, and publishes <c>TransactionCommitted</c>/<c>TransactionRolledBack</c> events.
/// </summary>
public interface ITransactionPipeline
{
    /// <summary>Executes the work within a transaction per <paramref name="options"/>.</summary>
    /// <typeparam name="TResult">The work result type.</typeparam>
    /// <param name="work">
    /// The command work; receives the active transaction (null for read-only) and the effective
    /// cancellation token (timeout- and cancellation-linked) so long work can observe it.
    /// </param>
    /// <param name="options">Transaction options (command identity, read-only flag, timeout).</param>
    TResult Execute<TResult>(Func<IWriteTransaction?, CancellationToken, TResult> work, TransactionOptions options);
}
