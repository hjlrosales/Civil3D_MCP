using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Errors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static Civil3D.Domain.Commands.Tests.TestDoubles;
using static Civil3D.Domain.Commands.Tests.TestCommands;

namespace Civil3D.Domain.Commands.Tests;

/// <summary>
/// The full dispatcher pipeline: validation aggregation, permission check, confirmation check,
/// transaction commit/rollback, domain event ordering, progress reporting and error mapping.
/// </summary>
public class CommandDispatcherTests
{
    private sealed record Harness(
        ICommandDispatcher Dispatcher,
        FakeTransactionProvider Provider,
        InMemoryDomainEventDispatcher Events,
        FakeWriteRepository Repository);

    private static Harness Create(
        CommandPermission effectivePermission = CommandPermission.ModifyDrawing,
        bool confirmationGranted = false)
    {
        var services = new ServiceCollection();
        var provider = new FakeTransactionProvider();
        var events = new InMemoryDomainEventDispatcher();
        var repository = new FakeWriteRepository();

        services.AddSingleton<ITransactionProvider>(provider);
        services.AddSingleton<IDomainEventDispatcher>(events);
        services.AddSingleton<ITransactionPipeline>(sp => new TransactionPipeline(
            sp.GetRequiredService<ITransactionProvider>(),
            sp.GetRequiredService<IDomainEventDispatcher>(),
            NullLogger<TransactionPipeline>.Instance));
        services.AddSingleton<ICommandDispatcher>(sp => new CommandDispatcher(
            sp,
            sp.GetRequiredService<ITransactionPipeline>(),
            sp.GetRequiredService<IDomainEventDispatcher>(),
            NullLogger<CommandDispatcher>.Instance));

        services.AddTransient<ICommandHandler<RecordWriteCommand, WriteCommandResult>, RecordWriteCommandHandler>();
        services.AddTransient<ICommandHandler<ConfirmationRequiredCommand, WriteCommandResult>, ConfirmationRequiredCommandHandler>();
        services.AddTransient<ICommandHandler<ReadOnlyProbeCommand, ProbeResult>, ReadOnlyProbeCommandHandler>();
        services.AddTransient<ICommandHandler<FailingCommand, WriteCommandResult>, FailingCommandHandler>();
        services.AddTransient<ICommandHandler<SlowCommand, WriteCommandResult>, SlowCommandHandler>();
        services.AddTransient<ICommandValidator<RecordWriteCommand>, ValueRequiredValidator>();
        services.AddTransient<ICommandValidator<RecordWriteCommand>, ValueMaxLengthValidator>();
        services.AddSingleton(repository);

        ServiceProvider container = services.BuildServiceProvider();
        return new Harness(
            container.GetRequiredService<ICommandDispatcher>(),
            provider,
            events,
            repository);
    }

    private static CommandExecutionContext Context(
        CommandPermission permission = CommandPermission.ModifyDrawing,
        bool confirmation = false,
        RecordingProgressReporter? progress = null)
        => new(
            CorrelationId: "c-1",
            SessionId: "s-1",
            CancellationToken: CancellationToken.None,
            Progress: progress ?? new RecordingProgressReporter(),
            Undo: NullUndoContext.Instance,
            EffectivePermission: permission,
            ConfirmationGranted: confirmation);

    [Fact]
    public async Task Dispatch_Success_CommitsAndPublishesEvents()
    {
        Harness harness = Create();

        WriteCommandResult result = await harness.Dispatcher.DispatchAsync<RecordWriteCommand, WriteCommandResult>(
            new RecordWriteCommand { Value = "ok" }, Context());

        Assert.True(result.HadTransaction);
        Assert.Equal(["ok"], harness.Repository.Writes);
        Assert.True(Assert.Single(harness.Provider.Begun).IsCommitted);

        Type[] order = harness.Events.Published.Select(e => e.GetType()).ToArray();
        Assert.Equal([typeof(CommandStarted), typeof(TransactionCommitted), typeof(CommandCompleted)], order);
    }

    [Fact]
    public async Task Dispatch_ValidationFailure_BlocksExecution()
    {
        Harness harness = Create();

        CommandException ex = await Assert.ThrowsAsync<CommandException>(() =>
            harness.Dispatcher.DispatchAsync<RecordWriteCommand, WriteCommandResult>(new RecordWriteCommand(), Context()));

        Assert.Equal(CommandErrorCode.ValidationFailed, ex.Code);
        Assert.Empty(harness.Repository.Writes);
        Assert.Empty(harness.Provider.Begun);
        Assert.Single(harness.Events.Published.OfType<CommandFailed>());
        Assert.Empty(harness.Events.Published.OfType<CommandCompleted>());
    }

    [Fact]
    public async Task Dispatch_MultipleValidators_AggregateFailures()
    {
        Harness harness = Create();

        CommandException ex = await Assert.ThrowsAsync<CommandException>(() =>
            harness.Dispatcher.DispatchAsync<RecordWriteCommand, WriteCommandResult>(new RecordWriteCommand { Value = null }, Context()));

        Assert.Equal(CommandErrorCode.ValidationFailed, ex.Code);
        Assert.Contains("must not be empty", ex.Message);
        Assert.Contains("between 1 and 10", ex.Message);
    }

    [Fact]
    public async Task Dispatch_PermissionDenied_BlocksExecution()
    {
        Harness harness = Create();

        CommandException ex = await Assert.ThrowsAsync<CommandException>(() =>
            harness.Dispatcher.DispatchAsync<RecordWriteCommand, WriteCommandResult>(
                new RecordWriteCommand { Value = "ok" },
                Context(permission: CommandPermission.ReadOnly)));

        Assert.Equal(CommandErrorCode.PermissionDenied, ex.Code);
        Assert.Empty(harness.Repository.Writes);
        Assert.Empty(harness.Provider.Begun);
    }

    [Fact]
    public async Task Dispatch_ConfirmationRequired_NotGranted_BlocksExecution()
    {
        Harness harness = Create();

        CommandException ex = await Assert.ThrowsAsync<CommandException>(() =>
            harness.Dispatcher.DispatchAsync<ConfirmationRequiredCommand, WriteCommandResult>(new ConfirmationRequiredCommand(), Context()));

        Assert.Equal(CommandErrorCode.ConfirmationRequired, ex.Code);
        Assert.Empty(harness.Repository.Writes);
        Assert.Empty(harness.Provider.Begun);
    }

    [Fact]
    public async Task Dispatch_ConfirmationGranted_Executes()
    {
        Harness harness = Create(confirmationGranted: true);

        WriteCommandResult result = await harness.Dispatcher.DispatchAsync<ConfirmationRequiredCommand, WriteCommandResult>(
            new ConfirmationRequiredCommand(),
            Context(confirmation: true));

        Assert.Equal(["confirmed-write"], harness.Repository.Writes);
        Assert.True(Assert.Single(harness.Provider.Begun).IsCommitted);
    }

    [Fact]
    public async Task Dispatch_ReadOnlyCommand_NeverBeginsTransaction()
    {
        Harness harness = Create();

        ProbeResult result = await harness.Dispatcher.DispatchAsync<ReadOnlyProbeCommand, ProbeResult>(
            new ReadOnlyProbeCommand(), Context());

        Assert.False(result.HadTransaction);
        Assert.Empty(harness.Provider.Begun);
        CommandCompleted completed = Assert.Single(harness.Events.Published.OfType<CommandCompleted>());
        Assert.False(completed.Committed);
    }

    [Fact]
    public async Task Dispatch_HandlerDomainFailure_RollsBackAndPropagates()
    {
        Harness harness = Create();

        DomainException ex = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Dispatcher.DispatchAsync<FailingCommand, WriteCommandResult>(new FailingCommand(), Context()));

        Assert.Equal(DomainErrorCode.TransactionFailed, ex.Code);
        Assert.True(Assert.Single(harness.Provider.Begun).IsRolledBack);
        Assert.Single(harness.Events.Published.OfType<TransactionRolledBack>());
        CommandFailed failed = Assert.Single(harness.Events.Published.OfType<CommandFailed>());
        Assert.Equal("TransactionFailed", failed.ErrorCode);
    }

    [Fact]
    public async Task Dispatch_ReportsProgressStages()
    {
        Harness harness = Create();
        var progress = new RecordingProgressReporter();

        await harness.Dispatcher.DispatchAsync<RecordWriteCommand, WriteCommandResult>(
            new RecordWriteCommand { Value = "ok" },
            Context(progress: progress));

        Assert.Equal(100, progress.Reports.Last().Percent);
        Assert.Equal([20, 40, 60, 100], progress.Reports.Select(r => r.Percent));
    }
}
