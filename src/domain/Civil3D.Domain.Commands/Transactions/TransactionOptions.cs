namespace Civil3D.Domain.Commands.Transactions;

/// <summary>Options for one <see cref="ITransactionPipeline.Execute"/> run.</summary>
public sealed record TransactionOptions
{
    /// <summary>The command executing in this transaction (for events and logging).</summary>
    public string CommandName { get; init; } = string.Empty;

    /// <summary>Correlation identifier of the originating request.</summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>
    /// True when the command is read-only: the pipeline invokes the work with a null transaction
    /// and never begins, commits or rolls back.
    /// </summary>
    public bool ReadOnly { get; init; }

    /// <summary>Maximum transaction duration; when exceeded the transaction is rolled back.</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>Effective cancellation token; cancellation rolls back the transaction.</summary>
    public CancellationToken CancellationToken { get; init; }
}
