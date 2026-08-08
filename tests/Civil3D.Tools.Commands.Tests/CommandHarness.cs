using Autodesk.Mcp.Sdk.Discovery;
using Civil3D.Domain.Commands;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static Civil3D.Tools.Commands.Tests.TestCommands;
using static Civil3D.Tools.Commands.Tests.TestDoubles;

namespace Civil3D.Tools.Commands.Tests;

/// <summary>
/// Builds the real command framework (CommandDispatcher + TransactionPipeline) over the in-memory
/// transaction provider, with the test handlers/validators registered, and exposes a
/// <see cref="ToolCatalog"/> over the test tools.
/// </summary>
internal static class CommandHarness
{
    internal sealed record Container(
        ServiceProvider Provider,
        RecordingTransactionProvider Transactions,
        InMemoryDomainEventDispatcher Events,
        FakeWriteRepository Repository);

    internal static Container CreateContainer(
        ICivil3DSession? session = null,
        IConfirmationGate? confirmationGate = null)
    {
        var services = new ServiceCollection();
        var transactions = new RecordingTransactionProvider();
        var events = new InMemoryDomainEventDispatcher();
        var repository = new FakeWriteRepository();

        services.AddSingleton<ITransactionProvider>(transactions);
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
        services.AddSingleton<IConfirmationGate>(confirmationGate ?? NullConfirmationGate.Instance);
        services.AddSingleton<IUndoContext>(_ => NullUndoContext.Instance);
        services.AddSingleton<ICivil3DSession>(session ?? new FakeSession(SampleDrawing()));

        services.AddTransient<ICommandHandler<RecordLogCommand, RecordLogResult>, RecordLogCommandHandler>();
        services.AddTransient<ICommandHandler<DestructiveCommand, RecordLogResult>, DestructiveCommandHandler>();
        services.AddTransient<ICommandHandler<FailingCommand, RecordLogResult>, FailingCommandHandler>();
        services.AddTransient<ICommandValidator<RecordLogCommand>, LabelRequiredValidator>();
        services.AddSingleton(repository);

        ServiceProvider provider = services.BuildServiceProvider();
        return new Container(provider, transactions, events, repository);
    }

    internal static ToolCatalog CreateCatalog(Container container)
        => new(
            new[] { typeof(RecordLogTool).Assembly },
            new ManifestGenerator(),
            container.Provider,
            NullLogger<ToolCatalog>.Instance);
}
