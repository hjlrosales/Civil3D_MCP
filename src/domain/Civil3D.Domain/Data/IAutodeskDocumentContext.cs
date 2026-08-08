namespace Civil3D.Domain.Data;

/// <summary>
/// Autodesk-free contract for executing read-only queries against the active Civil 3D document.
/// The bridge registers a real implementation that resolves the active drawing; tests substitute
/// an in-memory fake. Discipline data sources (<c>Autodesk*DataSource</c>) depend on this contract
/// so repository and service layers never touch the Autodesk API directly.
/// </summary>
public interface IAutodeskDocumentContext
{
    /// <summary>True when a drawing is currently open and queryable.</summary>
    bool HasActiveDocument { get; }

    /// <summary>
    /// Executes <paramref name="read"/> against the active drawing. The delegate receives the
    /// active <c>Autodesk.AutoCAD.DatabaseServices.Database</c> as <see cref="object"/> (keeping
    /// this contract Autodesk-free); data sources cast it to the Autodesk type and open a single
    /// read-only transaction of their own. Throws <see cref="Civil3D.Domain.Errors.DomainException"/>
    /// with <c>NoActiveDocument</c> when no drawing is open; other Autodesk failures are mapped to
    /// <c>TransactionFailed</c>.
    /// </summary>
    /// <typeparam name="T">The result type produced by the read.</typeparam>
    /// <param name="read">The query body; receives the active database.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    T ExecuteRead<T>(Func<object, T> read, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the active drawing database for a write operation. Write repositories use this
    /// (instead of <see cref="ExecuteRead"/>) to obtain the database while the command pipeline's
    /// own write transaction performs the actual changes; it applies the same document-availability
    /// and exception mapping as <see cref="ExecuteRead"/>.
    /// </summary>
    /// <typeparam name="T">The result type produced by the write body.</typeparam>
    /// <param name="write">The write body; receives the active database.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    T ExecuteWrite<T>(Func<object, T> write, CancellationToken cancellationToken = default);
}
