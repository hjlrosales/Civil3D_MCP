using Autodesk.Mcp.Sdk.Discovery;
using Civil3D.Domain.Commands;
using Civil3D.Domain.Surfaces.Services;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Surface.Dtos;
using Civil3D.Tools.Surface.Tools;
using Civil3D.Tools.Surface.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static Civil3D.Tools.Surface.Tests.TestDoubles;

namespace Civil3D.Tools.Surface.Tests;

/// <summary>
/// Builds the real workflow framework over the in-memory fakes: the actual
/// <see cref="WorkflowDispatcher"/>, the production <see cref="SurfaceComparisonWorkflowHandler"/>,
/// a fake session and the fake surface service. Also exposes a <see cref="ToolCatalog"/> over the
/// surface tool assembly for SDK-level tests.
/// </summary>
internal static class SurfaceHarness
{
    internal sealed record Container(
        ServiceProvider Provider,
        InMemoryDomainEventDispatcher Events,
        RecordingProgressReporter Progress);

    internal static Container CreateContainer(
        ICivil3DSession? session = null,
        ISurfaceService? surfaces = null)
    {
        var services = new ServiceCollection();
        var events = new InMemoryDomainEventDispatcher();
        var progress = new RecordingProgressReporter();

        services.AddSingleton<IDomainEventDispatcher>(events);
        services.AddSingleton<IWorkflowDispatcher>(sp => new WorkflowDispatcher(
            sp,
            sp.GetRequiredService<IDomainEventDispatcher>(),
            NullLogger<WorkflowDispatcher>.Instance));
        services.AddSingleton<ICivil3DSession>(session ?? new FakeSession(SampleData.Drawing()));
        services.AddSingleton<ISurfaceService>(surfaces ?? new FakeSurfaceService(SampleData.Contrasting()));
        services.AddSingleton<IWorkflowHandler<SurfaceComparisonWorkflow, SurfaceComparisonReport>,
            SurfaceComparisonWorkflowHandler>();

        ServiceProvider provider = services.BuildServiceProvider();
        return new Container(provider, events, progress);
    }

    /// <summary>Builds a workflow context for direct dispatcher tests.</summary>
    internal static WorkflowContext SurfaceContext(
        ServiceProvider provider,
        CancellationToken cancellationToken = default,
        RecordingProgressReporter? progress = null)
        => new(
            WorkflowName: "surface.comparison.report",
            CorrelationId: "c-surface",
            SessionId: "s-surface",
            CancellationToken: cancellationToken,
            Progress: new WorkflowProgress(progress ?? new RecordingProgressReporter()),
            Logger: NullLogger.Instance,
            Services: provider,
            Configuration: new Dictionary<string, string>(),
            EffectivePermission: CommandPermission.ReadOnly,
            StartedAtUtc: DateTimeOffset.UtcNow);

    internal static ToolCatalog CreateCatalog(Container container)
        => new(
            new[] { typeof(SurfaceComparisonReportTool).Assembly },
            new ManifestGenerator(),
            container.Provider,
            NullLogger<ToolCatalog>.Instance);
}
