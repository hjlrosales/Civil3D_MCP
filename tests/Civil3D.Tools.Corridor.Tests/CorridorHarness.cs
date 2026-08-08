using Autodesk.Mcp.Sdk.Discovery;
using Civil3D.Domain.Commands;
using Civil3D.Domain.Corridors.Services;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Corridor.Dtos;
using Civil3D.Tools.Corridor.Tools;
using Civil3D.Tools.Corridor.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static Civil3D.Tools.Corridor.Tests.TestDoubles;

namespace Civil3D.Tools.Corridor.Tests;

/// <summary>
/// Builds the real workflow framework over the in-memory fakes: the actual
/// <see cref="WorkflowDispatcher"/>, the production <see cref="CorridorAnalysisWorkflowHandler"/>,
/// a fake session and the fake corridor service. Also exposes a <see cref="ToolCatalog"/> over
/// the corridor tool assembly for SDK-level tests.
/// </summary>
internal static class CorridorHarness
{
    internal sealed record Container(
        ServiceProvider Provider,
        InMemoryDomainEventDispatcher Events,
        RecordingProgressReporter Progress,
        FakeCorridorService Corridors);

    internal static Container CreateContainer(
        ICivil3DSession? session = null,
        ICorridorService? corridors = null)
    {
        var services = new ServiceCollection();
        var events = new InMemoryDomainEventDispatcher();
        var progress = new RecordingProgressReporter();
        var fake = corridors as FakeCorridorService ?? new FakeCorridorService(SampleData.All());

        services.AddSingleton<IDomainEventDispatcher>(events);
        services.AddSingleton<IWorkflowDispatcher>(sp => new WorkflowDispatcher(
            sp,
            sp.GetRequiredService<IDomainEventDispatcher>(),
            NullLogger<WorkflowDispatcher>.Instance));
        services.AddSingleton<ICivil3DSession>(session ?? new FakeSession(SampleData.Drawing()));
        services.AddSingleton<ICorridorService>(corridors ?? fake);
        services.AddSingleton<IWorkflowHandler<CorridorAnalysisWorkflow, CorridorAnalysisReport>,
            CorridorAnalysisWorkflowHandler>();

        ServiceProvider provider = services.BuildServiceProvider();
        return new Container(provider, events, progress, corridors as FakeCorridorService ?? fake);
    }

    /// <summary>Builds a workflow context for direct dispatcher tests.</summary>
    internal static WorkflowContext CorridorContext(
        ServiceProvider provider,
        CancellationToken cancellationToken = default,
        RecordingProgressReporter? progress = null)
        => new(
            WorkflowName: "corridor.analysis.report",
            CorrelationId: "c-corridor",
            SessionId: "s-corridor",
            CancellationToken: cancellationToken,
            Progress: new WorkflowProgress(progress ?? new RecordingProgressReporter()),
            Logger: NullLogger.Instance,
            Services: provider,
            Configuration: new Dictionary<string, string>(),
            EffectivePermission: CommandPermission.ReadOnly,
            StartedAtUtc: DateTimeOffset.UtcNow);

    internal static ToolCatalog CreateCatalog(Container container)
        => new(
            new[] { typeof(CorridorAnalysisReportTool).Assembly },
            new ManifestGenerator(),
            container.Provider,
            NullLogger<ToolCatalog>.Instance);
}
