using Autodesk.Mcp.Sdk.Discovery;
using Civil3D.Domain.Commands;
using Civil3D.Domain.Surfaces.Services;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.CutFill.Abstractions;
using Civil3D.Tools.CutFill.Dtos;
using Civil3D.Tools.CutFill.Tools;
using Civil3D.Tools.CutFill.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static Civil3D.Tools.CutFill.Tests.TestDoubles;

namespace Civil3D.Tools.CutFill.Tests;

/// <summary>
/// Builds the real workflow framework over the in-memory fakes: the actual
/// <see cref="WorkflowDispatcher"/>, the production <see cref="CutFillWorkflowHandler"/>, a fake
/// session, the fake surface service and the fake (or production) calculator. Also exposes a
/// <see cref="ToolCatalog"/> over the cut/fill tool assembly for SDK-level tests.
/// </summary>
internal static class CutFillHarness
{
    internal sealed record Container(
        ServiceProvider Provider,
        InMemoryDomainEventDispatcher Events,
        RecordingProgressReporter Progress,
        FakeCutFillCalculator Calculator);

    internal static Container CreateContainer(
        ICivil3DSession? session = null,
        ISurfaceService? surfaces = null,
        ICutFillCalculator? calculator = null)
    {
        var services = new ServiceCollection();
        var events = new InMemoryDomainEventDispatcher();
        var progress = new RecordingProgressReporter();
        var fake = calculator as FakeCutFillCalculator ?? new FakeCutFillCalculator(SampleData.CutDominant());

        services.AddSingleton<IDomainEventDispatcher>(events);
        services.AddSingleton<IWorkflowDispatcher>(sp => new WorkflowDispatcher(
            sp,
            sp.GetRequiredService<IDomainEventDispatcher>(),
            NullLogger<WorkflowDispatcher>.Instance));
        services.AddSingleton<ICivil3DSession>(session ?? new FakeSession(SampleData.Drawing()));
        services.AddSingleton<ISurfaceService>(surfaces ?? new FakeSurfaceService(SampleData.Contrasting()));
        services.AddSingleton<ICutFillCalculator>(calculator ?? fake);
        services.AddSingleton<IWorkflowHandler<CutFillWorkflow, CutFillReport>, CutFillWorkflowHandler>();

        ServiceProvider provider = services.BuildServiceProvider();
        return new Container(provider, events, progress, calculator as FakeCutFillCalculator ?? fake);
    }

    /// <summary>Builds a workflow context for direct dispatcher tests.</summary>
    internal static WorkflowContext CutFillContext(
        ServiceProvider provider,
        CancellationToken cancellationToken = default,
        RecordingProgressReporter? progress = null)
        => new(
            WorkflowName: "calculate.cut.fill",
            CorrelationId: "c-cutfill",
            SessionId: "s-cutfill",
            CancellationToken: cancellationToken,
            Progress: new WorkflowProgress(progress ?? new RecordingProgressReporter()),
            Logger: NullLogger.Instance,
            Services: provider,
            Configuration: new Dictionary<string, string>(),
            EffectivePermission: CommandPermission.ReadOnly,
            StartedAtUtc: DateTimeOffset.UtcNow);

    internal static ToolCatalog CreateCatalog(Container container)
        => new(
            new[] { typeof(CalculateCutFillTool).Assembly },
            new ManifestGenerator(),
            container.Provider,
            NullLogger<ToolCatalog>.Instance);
}
