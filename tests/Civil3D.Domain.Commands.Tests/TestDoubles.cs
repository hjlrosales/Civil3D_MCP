using Civil3D.Domain.Commands.Transactions;

namespace Civil3D.Domain.Commands.Tests;

/// <summary>
/// In-memory write transaction and provider, plus a repository and progress recorder used by the
/// pipeline/dispatcher tests. The fake transaction enforces the same state machine the Autodesk
/// implementation will (Active → Committed | RolledBack → Disposed).
/// </summary>
internal static class TestDoubles
{
    internal sealed class FakeWriteTransaction : IWriteTransaction
    {
        internal string CommandName { get; init; } = string.Empty;

        public object? Handle { get; } = "fake";
        public bool IsCommitted { get; private set; }
        public bool IsRolledBack { get; private set; }
        public bool IsDisposed { get; private set; }
        public int CommitCount { get; private set; }
        public int RollbackCount { get; private set; }

        public void Commit()
        {
            if (IsCommitted)
            {
                throw new CommandException(CommandErrorCode.TransactionFailed, "Commit after commit.");
            }

            if (IsDisposed)
            {
                throw new CommandException(CommandErrorCode.TransactionFailed, "Commit after dispose.");
            }

            IsCommitted = true;
            CommitCount++;
        }

        public void Rollback()
        {
            if (IsCommitted)
            {
                throw new CommandException(CommandErrorCode.TransactionFailed, "Rollback after commit.");
            }

            if (IsDisposed)
            {
                throw new CommandException(CommandErrorCode.TransactionFailed, "Rollback after dispose.");
            }

            IsRolledBack = true;
            RollbackCount++;
        }

        public void Dispose() => IsDisposed = true;
    }

    internal sealed class FakeTransactionProvider : ITransactionProvider
    {
        public List<FakeWriteTransaction> Begun { get; } = [];

        public IWriteTransaction Begin(string commandName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var transaction = new FakeWriteTransaction { CommandName = commandName };
            Begun.Add(transaction);
            return transaction;
        }
    }

    /// <summary>A transaction whose Commit always fails (models an Autodesk commit failure); rollback succeeds.</summary>
    internal sealed class FailingCommitTransaction : IWriteTransaction
    {
        public object? Handle { get; } = "failing-commit";
        public bool IsCommitted { get; private set; }
        public bool IsRolledBack { get; private set; }
        public bool IsDisposed { get; private set; }

        public void Commit() => throw new CommandException(CommandErrorCode.TransactionFailed, "commit failed");

        public void Rollback() => IsRolledBack = true;

        public void Dispose() => IsDisposed = true;
    }

    internal sealed class FailingCommitProvider : ITransactionProvider
    {
        public List<FailingCommitTransaction> Begun { get; } = [];

        public IWriteTransaction Begin(string commandName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var transaction = new FailingCommitTransaction();
            Begun.Add(transaction);
            return transaction;
        }
    }

    /// <summary>A repository the test handlers write to (stands in for a discipline repository).</summary>
    internal sealed class FakeWriteRepository
    {
        public List<string> Writes { get; } = [];
    }

    /// <summary>Records progress reports for assertions.</summary>
    internal sealed class RecordingProgressReporter : IProgressReporter
    {
        public List<(int Percent, string? Stage)> Reports { get; } = [];

        public void Report(int percent, string? stage = null, string? message = null)
            => Reports.Add((percent, stage));
    }
}
