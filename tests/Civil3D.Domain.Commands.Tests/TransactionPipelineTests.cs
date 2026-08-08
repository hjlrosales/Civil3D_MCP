using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Errors;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static Civil3D.Domain.Commands.Tests.TestDoubles;
using static Civil3D.Domain.Commands.Tests.TestCommands;

namespace Civil3D.Domain.Commands.Tests;

/// <summary>
/// The write transaction pipeline: commit on success, rollback on any failure, nested detection,
/// read-only detection, timeout, cancellation and automatic disposal.
/// </summary>
public class TransactionPipelineTests
{
    private static TransactionPipeline Create(
        ITransactionProvider provider,
        InMemoryDomainEventDispatcher? events = null)
        => new(
            provider,
            events ?? new InMemoryDomainEventDispatcher(),
            NullLogger<TransactionPipeline>.Instance);

    private static TransactionOptions Options(
        string command = "test",
        bool readOnly = false,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
        => new()
        {
            CommandName = command,
            CorrelationId = "c-1",
            ReadOnly = readOnly,
            Timeout = timeout,
            CancellationToken = cancellationToken,
        };

    [Fact]
    public void Execute_CommitsAndDisposes_OnSuccess()
    {
        var provider = new FakeTransactionProvider();
        TransactionPipeline pipeline = Create(provider);

        WriteCommandResult result = pipeline.Execute(
            (transaction, _) => new WriteCommandResult("ok", transaction is not null),
            Options("record.write"));

        Assert.True(result.HadTransaction);
        FakeWriteTransaction tx = Assert.Single(provider.Begun);
        Assert.True(tx.IsCommitted);
        Assert.False(tx.IsRolledBack);
        Assert.True(tx.IsDisposed);
    }

    [Fact]
    public void Execute_HandlerFailure_RollsBackAndRethrows()
    {
        var provider = new FakeTransactionProvider();
        var events = new InMemoryDomainEventDispatcher();
        TransactionPipeline pipeline = Create(provider, events);

        DomainException ex = Assert.Throws<DomainException>(() => pipeline.Execute<WriteCommandResult>(
            (_, _) => throw new DomainException(DomainErrorCode.TransactionFailed, "boom"),
            Options("fail.write")));

        Assert.Equal(DomainErrorCode.TransactionFailed, ex.Code);
        FakeWriteTransaction tx = Assert.Single(provider.Begun);
        Assert.True(tx.IsRolledBack);
        Assert.False(tx.IsCommitted);
        Assert.True(tx.IsDisposed);
        Assert.Single(events.Published.OfType<TransactionRolledBack>());
        Assert.Empty(events.Published.OfType<TransactionCommitted>());
    }

    [Fact]
    public void Execute_ReadOnly_NeverBeginsATransaction()
    {
        var provider = new FakeTransactionProvider();
        TransactionPipeline pipeline = Create(provider);

        ProbeResult result = pipeline.Execute(
            (transaction, _) => new ProbeResult("probe.read", transaction is not null),
            Options("probe.read", readOnly: true));

        Assert.False(result.HadTransaction);
        Assert.Empty(provider.Begun);
    }

    [Fact]
    public void Execute_Nested_ThrowsTransactionAlreadyActive()
    {
        var provider = new FakeTransactionProvider();
        TransactionPipeline pipeline = Create(provider);

        CommandException ex = Assert.Throws<CommandException>(() => pipeline.Execute<WriteCommandResult>(
            (_, _) => pipeline.Execute(
                (inner, _) => new WriteCommandResult("nested", inner is not null),
                Options("nested")),
            Options("outer")));

        Assert.Equal(CommandErrorCode.TransactionAlreadyActive, ex.Code);
        // The outer transaction was rolled back and disposed.
        FakeWriteTransaction outer = Assert.Single(provider.Begun);
        Assert.True(outer.IsRolledBack);
        Assert.True(outer.IsDisposed);
    }

    [Fact]
    public void Execute_Timeout_RollsBackAndThrowsTransactionTimeout()
    {
        var provider = new FakeTransactionProvider();
        var events = new InMemoryDomainEventDispatcher();
        TransactionPipeline pipeline = Create(provider, events);

        CommandException ex = Assert.Throws<CommandException>(() => pipeline.Execute<WriteCommandResult>(
            (transaction, token) =>
            {
                var slow = new SlowCommand();
                var handler = new SlowCommandHandler();
                return handler.Handle(slow, null!, transaction, token);
            },
            Options("slow.write", timeout: TimeSpan.FromMilliseconds(80))));

        Assert.Equal(CommandErrorCode.TransactionTimeout, ex.Code);
        FakeWriteTransaction tx = Assert.Single(provider.Begun);
        Assert.True(tx.IsRolledBack);
        TransactionRolledBack rolledBack = Assert.Single(events.Published.OfType<TransactionRolledBack>());
        Assert.Equal("timeout", rolledBack.Reason);
    }

    [Fact]
    public async Task Execute_Cancellation_RollsBackAndThrowsCancelled()
    {
        var provider = new FakeTransactionProvider();
        var events = new InMemoryDomainEventDispatcher();
        TransactionPipeline pipeline = Create(provider, events);
        using var cts = new CancellationTokenSource();

        Task<CommandException?> task = Task.Run(() =>
        {
            try
            {
                pipeline.Execute<WriteCommandResult>(
                    (transaction, token) =>
                    {
                        var slow = new SlowCommand();
                        var handler = new SlowCommandHandler();
                        return handler.Handle(slow, null!, transaction, token);
                    },
                    Options("slow.write", cancellationToken: cts.Token));
                return null;
            }
            catch (CommandException ex)
            {
                return ex;
            }
        });

        await Task.Delay(80);
        cts.Cancel();
        CommandException? caught = await task;

        Assert.NotNull(caught);
        Assert.Equal(CommandErrorCode.Cancelled, caught!.Code);
        FakeWriteTransaction tx = Assert.Single(provider.Begun);
        Assert.True(tx.IsRolledBack);
        TransactionRolledBack rolledBack = Assert.Single(events.Published.OfType<TransactionRolledBack>());
        Assert.Equal("cancelled", rolledBack.Reason);
    }

    [Fact]
    public void Execute_CommitFailure_RollsBack()
    {
        var provider = new FailingCommitProvider();
        TransactionPipeline pipeline = Create(provider);

        // The provider's transaction fails at Commit (like an Autodesk commit failure); the
        // pipeline must roll back and surface the failure.
        CommandException ex = Assert.Throws<CommandException>(() => pipeline.Execute<WriteCommandResult>(
            (transaction, _) => new WriteCommandResult("ok", transaction is not null),
            Options("bad.write")));

        Assert.Equal(CommandErrorCode.TransactionFailed, ex.Code);
        FailingCommitTransaction tx = Assert.Single(provider.Begun);
        Assert.True(tx.IsRolledBack);
        Assert.True(tx.IsDisposed);
    }
}
