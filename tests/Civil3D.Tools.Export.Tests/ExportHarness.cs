using Autodesk.Mcp.Sdk.Discovery;
using Civil3D.Domain.Alignments.Services;
using Civil3D.Domain.Commands;
using Civil3D.Domain.Corridors.Services;
using Civil3D.Domain.Pipes.Services;
using Civil3D.Domain.Profiles.Services;
using Civil3D.Domain.Surfaces.Services;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Export.Abstractions;
using Civil3D.Tools.Export.Dtos;
using Civil3D.Tools.Export.Tools;
using Civil3D.Tools.Export.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static Civil3D.Tools.Export.Tests.TestDoubles;

namespace Civil3D.Tools.Export.Tests;

/// <summary>
/// Builds the real workflow framework over the in-memory fakes: the actual
/// <see cref="WorkflowDispatcher"/>, the production <see cref="LandXmlExportWorkflowHandler"/>,
/// a fake session, the counting domain services and the fake exporter. Workflow contexts default
/// to <see cref="CommandPermission.Export"/> because the export workflow requires it. Also
/// exposes a <see cref="ToolCatalog"/> over the export tool assembly for SDK-level tests.
/// </summary>
internal static class ExportHarness
{
    internal sealed record Container(
        ServiceProvider Provider,
        InMemoryDomainEventDispatcher Events,
        RecordingProgressReporter Progress,
        FakeLandXmlExporter Exporter);

    internal static Container CreateContainer(
        ICivil3DSession? session = null,
        ILandXmlExporter? exporter = null,
        IAlignmentService? alignments = null,
        IProfileService? profiles = null,
        ISurfaceService? surfaces = null,
        ICorridorService? corridors = null,
        IPipeService? pipes = null)
    {
        var services = new ServiceCollection();
        var events = new InMemoryDomainEventDispatcher();
        var progress = new RecordingProgressReporter();
        var fake = exporter as FakeLandXmlExporter ?? new FakeLandXmlExporter();

        services.AddSingleton<IDomainEventDispatcher>(events);
        services.AddSingleton<IWorkflowDispatcher>(sp => new WorkflowDispatcher(
            sp,
            sp.GetRequiredService<IDomainEventDispatcher>(),
            NullLogger<WorkflowDispatcher>.Instance));
        services.AddSingleton<ICivil3DSession>(session ?? new FakeSession(SampleData.Drawing()));
        services.AddSingleton<ILandXmlExporter>(exporter ?? fake);
        services.AddSingleton<IAlignmentService>(alignments ?? SampleData.Alignments());
        services.AddSingleton<IProfileService>(profiles ?? SampleData.Profiles());
        services.AddSingleton<ISurfaceService>(surfaces ?? SampleData.Surfaces());
        services.AddSingleton<ICorridorService>(corridors ?? SampleData.Corridors());
        services.AddSingleton<IPipeService>(pipes ?? SampleData.PipeNetworks());
        services.AddSingleton<IWorkflowHandler<LandXmlExportWorkflow, LandXmlExportReport>,
            LandXmlExportWorkflowHandler>();

        ServiceProvider provider = services.BuildServiceProvider();
        return new Container(provider, events, progress, exporter as FakeLandXmlExporter ?? fake);
    }

    /// <summary>Builds a workflow context for direct dispatcher tests (Export permission).</summary>
    internal static WorkflowContext ExportContext(
        ServiceProvider provider,
        CancellationToken cancellationToken = default,
        RecordingProgressReporter? progress = null,
        CommandPermission permission = CommandPermission.Export)
        => new(
            WorkflowName: "landxml.export",
            CorrelationId: "c-export",
            SessionId: "s-export",
            CancellationToken: cancellationToken,
            Progress: new WorkflowProgress(progress ?? new RecordingProgressReporter()),
            Logger: NullLogger.Instance,
            Services: provider,
            Configuration: new Dictionary<string, string>(),
            EffectivePermission: permission,
            StartedAtUtc: DateTimeOffset.UtcNow);

    internal static ToolCatalog CreateCatalog(Container container)
        => new(
            new[] { typeof(ExportLandXmlTool).Assembly },
            new ManifestGenerator(),
            container.Provider,
            NullLogger<ToolCatalog>.Instance);
}
