using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Civil3D.Domain.Commands;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Errors;
using CoreApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Civil3D.Bridge.Data;

/// <summary>
/// Real <see cref="ITransactionProvider"/>: resolves the active drawing, locks the document for
/// writing and begins a <see cref="Transaction"/> via the database's TransactionManager. The
/// returned handle exposes the Autodesk transaction to the command handler (Phase 5B repositories
/// call <c>GetObject</c> on it) while the pipeline owns commit/rollback/disposal. Must only be
/// invoked on the application context (the tool dispatcher guarantees this).
/// </summary>
public sealed class AutodeskTransactionProvider : ITransactionProvider
{
    /// <inheritdoc />
    public IWriteTransaction Begin(string commandName, CancellationToken cancellationToken)
    {
        Document? document = CoreApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            throw new CommandException(
                CommandErrorCode.NoActiveDocument,
                "No active document is available to operate on.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return new AutodeskWriteTransaction(document, cancellationToken);
        }
        catch (CommandException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CommandException(
                CommandErrorCode.TransactionFailed,
                "The write transaction could not be started.",
                ex);
        }
    }

    private sealed class AutodeskWriteTransaction : IWriteTransaction
    {
        private readonly Document _document;
        private readonly DocumentLock _documentLock;
        private readonly Transaction _transaction;
        private bool _committed;
        private bool _rolledBack;
        private bool _disposed;

        internal AutodeskWriteTransaction(Document document, CancellationToken cancellationToken)
        {
            _document = document;
            cancellationToken.ThrowIfCancellationRequested();
            _documentLock = document.LockDocument();
            try
            {
                _transaction = document.Database.TransactionManager.StartTransaction();
            }
            catch
            {
                _documentLock.Dispose();
                throw;
            }
        }

        /// <inheritdoc />
        public object? Handle => _transaction;

        /// <inheritdoc />
        public bool IsCommitted => _committed;

        /// <inheritdoc />
        public bool IsRolledBack => _rolledBack;

        /// <inheritdoc />
        public bool IsDisposed => _disposed;

        /// <inheritdoc />
        public void Commit()
        {
            if (_committed || _rolledBack || _disposed)
            {
                throw new CommandException(
                    CommandErrorCode.TransactionFailed,
                    "The transaction is no longer active and cannot be committed.");
            }

            try
            {
                _transaction.Commit();
                _committed = true;
            }
            catch (Exception ex)
            {
                throw new CommandException(
                    CommandErrorCode.TransactionFailed,
                    "The write transaction failed to commit.",
                    ex);
            }
        }

        /// <inheritdoc />
        public void Rollback()
        {
            if (_committed || _disposed)
            {
                return; // Nothing to undo.
            }

            if (_rolledBack)
            {
                return;
            }

            try
            {
                _transaction.Abort();
                _rolledBack = true;
            }
            catch
            {
                _rolledBack = true; // Best effort; the pipeline still publishes the rollback event.
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _transaction.Dispose();
            _documentLock.Dispose();
            _disposed = true;
        }
    }
}
