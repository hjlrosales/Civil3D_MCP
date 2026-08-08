namespace Civil3D.Domain.Commands.Transactions;

/// <summary>
/// Creates <see cref="IWriteTransaction"/> instances for the pipeline. The bridge registers the
/// real Autodesk-backed provider (active document, document lock, TransactionManager); tests use
/// an in-memory fake. The provider is a seam: the pipeline never touches Autodesk APIs.
/// </summary>
public interface ITransactionProvider
{
    /// <summary>
    /// Begins a write transaction against the active drawing.
    /// </summary>
    /// <param name="commandName">The command the transaction belongs to (for diagnostics).</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    IWriteTransaction Begin(string commandName, CancellationToken cancellationToken);
}
