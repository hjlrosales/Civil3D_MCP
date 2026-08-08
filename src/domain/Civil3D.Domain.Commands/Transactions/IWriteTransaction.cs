namespace Civil3D.Domain.Commands.Transactions;

/// <summary>
/// A write transaction handle. The pipeline begins one per writing command, hands it to the
/// handler, and commits it when the handler succeeds. Implementations guard their state machine
/// (Active → Committed | RolledBack → Disposed) and throw <see cref="CommandException"/>
/// (<c>TransactionFailed</c>) on illegal transitions. The real Autodesk implementation lives in
/// the Bridge; tests use an in-memory fake.
/// </summary>
public interface IWriteTransaction : IDisposable
{
    /// <summary>The underlying host transaction, when the implementation exposes one (else null).</summary>
    object? Handle { get; }

    /// <summary>True after <see cref="Commit"/> succeeded.</summary>
    bool IsCommitted { get; }

    /// <summary>True after <see cref="Rollback"/> was requested.</summary>
    bool IsRolledBack { get; }

    /// <summary>True once the transaction has been disposed.</summary>
    bool IsDisposed { get; }

    /// <summary>Commits the transaction. Throws after commit, rollback or disposal.</summary>
    void Commit();

    /// <summary>Rolls the transaction back. Throws after commit or disposal.</summary>
    void Rollback();
}
