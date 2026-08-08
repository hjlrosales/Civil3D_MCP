using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Tools;
using Civil3D.Domain.Alignments.Repositories;
using Civil3D.Domain.Alignments.Services;
using Civil3D.Domain.Commands;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Surfaces.Repositories;
using Civil3D.Domain.Surfaces.Services;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Commands;
using Civil3D.Tools.Editing.Commands;
using Civil3D.Tools.Editing.Tools;
using Civil3D.Tools.Editing.Validators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Civil3D.Tools.Editing.Tests;

/// <summary>
/// Builds the real command framework (CommandDispatcher + TransactionPipeline) over the
/// in-memory drawing and its fake repositories, registers the rename handlers and validators,
/// and exposes the two production rename tools plus a <see cref="ToolCatalog"/> for discovery.
/// </summary>
internal static class EditingTestHarness
{
    internal sealed record Container(
        ServiceProvider Provider,
        InMemoryDrawing Drawing,
        InMemoryDomainEventDispatcher Events,
        RecordingUndoContext Undo,
        RenameAlignmentTool AlignmentTool,
        RenameSurfaceTool SurfaceTool);

    internal static Container Create(
        InMemoryDrawing? drawing = null,
        IConfirmationGate? confirmationGate = null,
        bool requireConfirmation = false,
        ICivil3DSession? session = null)
    {
        drawing ??= new InMemoryDrawing(
            alignments: [(1, "Mainline"), (2, "Ramp A")],
            surfaces: [(10, "EG"), (20, "FG")]);

        var services = new ServiceCollection();
        var events = new InMemoryDomainEventDispatcher();
        var undo = new RecordingUndoContext();

        services.AddSingleton(drawing);
        services.AddSingleton<IAlignmentRepository, FakeAlignmentRepository>();
        services.AddSingleton<IAlignmentService, AlignmentService>();
        services.AddSingleton<IAlignmentRenameRepository, FakeAlignmentRenameRepository>();
        services.AddSingleton<IRenameAlignmentService, RenameAlignmentService>();
        services.AddSingleton<ISurfaceRepository, FakeSurfaceRepository>();
        services.AddSingleton<ISurfaceService, SurfaceService>();
        services.AddSingleton<ISurfaceRenameRepository, FakeSurfaceRenameRepository>();
        services.AddSingleton<IRenameSurfaceService, RenameSurfaceService>();
        services.AddSingleton<IDomainEventDispatcher>(events);
        services.AddSingleton<IUndoContext>(undo);
        services.AddSingleton<ITransactionProvider>(sp => new FakeTransactionProvider(drawing));
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
        services.AddSingleton<ICivil3DSession>(session ?? new FakeSession());

        services.AddTransient<ICommandHandler<RenameAlignmentCommand, RenameResult>>(sp =>
            new RenameCommandHandler<RenameAlignmentCommand>(
                (transaction, id, newName, context) =>
                    sp.GetRequiredService<IRenameAlignmentService>().Rename(transaction, id, newName, context)));
        services.AddTransient<ICommandHandler<RenameSurfaceCommand, RenameResult>>(sp =>
            new RenameCommandHandler<RenameSurfaceCommand>(
                (transaction, id, newName, context) =>
                    sp.GetRequiredService<IRenameSurfaceService>().Rename(transaction, id, newName, context)));
        services.AddTransient<ICommandValidator<RenameAlignmentCommand>, RenameAlignmentCommandValidator>();
        services.AddTransient<ICommandValidator<RenameSurfaceCommand>, RenameSurfaceCommandValidator>();

        ServiceProvider provider = services.BuildServiceProvider();
        var tools = new Container(
            provider,
            drawing,
            events,
            undo,
            new RenameAlignmentTool(
                provider.GetRequiredService<ICivil3DSession>(),
                provider.GetRequiredService<ICommandDispatcher>(),
                provider.GetRequiredService<IAlignmentService>(),
                provider.GetRequiredService<IConfirmationGate>(),
                provider.GetRequiredService<IUndoContext>(),
                requireConfirmation),
            new RenameSurfaceTool(
                provider.GetRequiredService<ICivil3DSession>(),
                provider.GetRequiredService<ICommandDispatcher>(),
                provider.GetRequiredService<ISurfaceService>(),
                provider.GetRequiredService<IConfirmationGate>(),
                provider.GetRequiredService<IUndoContext>(),
                requireConfirmation));

        return tools;
    }

    internal static ToolCatalog CreateCatalog(Container container)
        => new(
            new[] { typeof(RenameAlignmentTool).Assembly },
            new ManifestGenerator(),
            container.Provider,
            NullLogger<ToolCatalog>.Instance);

    /// <summary>Records undo unit registrations and their commit/rollback state.</summary>
    internal sealed class RecordingUndoContext : IUndoContext
    {
        public List<(string Description, bool Committed, bool RolledBack)> Units { get; } = [];

        public IUndoUnit Begin(string description)
        {
            var unit = new RecordingUndoUnit(() => Units.Add((description, true, false)));
            return unit;
        }

        private sealed class RecordingUndoUnit : IUndoUnit
        {
            private readonly Action _onCommit;
            public RecordingUndoUnit(Action onCommit) => _onCommit = onCommit;
            public void Commit() => _onCommit();
            public void Rollback() { }
            public void Dispose() { }
        }
    }
}

/// <summary>A fake write transaction provider bound to the in-memory drawing.</summary>
internal sealed class FakeTransactionProvider(InMemoryDrawing drawing) : ITransactionProvider
{
    public IWriteTransaction Begin(string commandName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new InMemoryDrawing.InMemoryWriteTransaction(drawing);
    }
}

/// <summary>A session that always reports an active drawing.</summary>
internal sealed class FakeSession : ICivil3DSession
{
    public ActiveDrawing? GetActiveDrawing() => new()
    {
        DrawingName = "EditingSample.dwg",
        DrawingPath = @"C:\Drawings\EditingSample.dwg",
        DrawingVersion = "AC1032",
        IsModified = false,
        IsReadOnly = false,
        CurrentLayout = "Model",
        IsModelSpaceActive = true,
        DatabaseFingerprint = "fp-editing",
        Civil3DVersion = "25.0",
        OpenDocumentsCount = 1,
        CurrentDocumentName = "EditingSample.dwg",
        CurrentDocumentPath = @"C:\Drawings\EditingSample.dwg",
    };
}

/// <summary>Grants every confirmation (for confirmation tests).</summary>
internal sealed class GrantingConfirmationGate : IConfirmationGate
{
    public bool IsGranted(ICommand command, string correlationId) => true;
}
