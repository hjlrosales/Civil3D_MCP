using Autodesk.Mcp.Sdk.Discovery;
using Civil3D.Domain.Alignments.Services;
using Civil3D.Domain.Cogo.Services;
using Civil3D.Domain.Commands;
using Civil3D.Domain.Corridors.Services;
using Civil3D.Domain.Pipes.Services;
using Civil3D.Domain.Profiles.Services;
using Civil3D.Domain.Styles.Services;
using Civil3D.Domain.Surfaces.Services;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Validation.Dtos;
using Civil3D.Tools.Validation.Framework;
using Civil3D.Tools.Validation.Rules;
using Civil3D.Tools.Validation.Tools;
using Civil3D.Tools.Validation.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static Civil3D.Tools.Validation.Tests.TestDoubles;

namespace Civil3D.Tools.Validation.Tests;

/// <summary>
/// Builds the real workflow framework and the real validation engine over the in-memory fakes:
/// the actual <see cref="WorkflowDispatcher"/>, the production <see cref="ValidationEngine"/> with
/// the eight rules, the production <see cref="DesignValidationWorkflowHandler"/>, a fake
/// session/statistics service and the seven fake domain services. Also exposes a
/// <see cref="ToolCatalog"/> over the validation tool assembly for SDK-level tests.
/// </summary>
internal static class ValidationHarness
{
    internal sealed record Container(
        ServiceProvider Provider,
        InMemoryDomainEventDispatcher Events,
        RecordingProgressReporter Progress);

    /// <summary>The eight rules registered in the production composition root.</summary>
    internal static IReadOnlyList<IValidationRule> DefaultRules() =>
    [
        new DuplicateNameRule(),
        new MissingDescriptionRule(),
        new EmptyCollectionRule(),
        new UnresolvedReferenceRule(),
        new UnusedStyleRule(),
        new DuplicateCogoPointNumberRule(),
        new ProfileWithoutAlignmentRule(),
        new PipeNetworkWithoutStructureRule(),
    ];

    internal static Container CreateContainer(
        ICivil3DSession? session = null,
        IDrawingStatisticsService? statistics = null,
        IAlignmentService? alignments = null,
        ISurfaceService? surfaces = null,
        IProfileService? profiles = null,
        ICorridorService? corridors = null,
        IPipeService? pipes = null,
        ICogoService? cogo = null,
        IStyleService? styles = null,
        IEnumerable<IValidationRule>? rules = null)
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
        services.AddSingleton<IDrawingStatisticsService>(statistics ?? new FakeDrawingStatisticsService(SampleData.Statistics()));
        services.AddSingleton<IAlignmentService>(alignments ?? new FakeAlignmentService(SampleData.Alignments()));
        services.AddSingleton<ISurfaceService>(surfaces ?? new FakeSurfaceService(SampleData.Surfaces()));
        services.AddSingleton<IProfileService>(profiles ?? new FakeProfileService(SampleData.Profiles()));
        services.AddSingleton<ICorridorService>(corridors ?? new FakeCorridorService(SampleData.Corridors()));
        services.AddSingleton<IPipeService>(pipes ?? new FakePipeService(SampleData.PipeNetworks()));
        services.AddSingleton<ICogoService>(cogo ?? new FakeCogoService(SampleData.CogoPoints()));
        services.AddSingleton<IStyleService>(styles ?? new FakeStyleService(SampleData.Styles()));

        // The engine discovers its rules through the container exactly like the bridge root.
        services.AddSingleton<IValidationEngine>(sp => new ValidationEngine(
            sp.GetServices<IValidationRule>(),
            NullLogger<ValidationEngine>.Instance));
        foreach (IValidationRule rule in rules ?? DefaultRules())
        {
            services.AddSingleton(rule);
        }

        services.AddSingleton<IWorkflowHandler<DesignValidationWorkflow, DesignValidationReport>,
            DesignValidationWorkflowHandler>();

        ServiceProvider provider = services.BuildServiceProvider();
        return new Container(provider, events, progress);
    }

    /// <summary>Builds a workflow context for direct dispatcher tests.</summary>
    internal static WorkflowContext ValidationContext(
        ServiceProvider provider,
        CancellationToken cancellationToken = default,
        RecordingProgressReporter? progress = null)
        => new(
            WorkflowName: "design.validation.report",
            CorrelationId: "c-validation",
            SessionId: "s-validation",
            CancellationToken: cancellationToken,
            Progress: new WorkflowProgress(progress ?? new RecordingProgressReporter()),
            Logger: NullLogger.Instance,
            Services: provider,
            Configuration: new Dictionary<string, string>(),
            EffectivePermission: CommandPermission.ReadOnly,
            StartedAtUtc: DateTimeOffset.UtcNow);

    internal static ToolCatalog CreateCatalog(Container container)
        => new(
            new[] { typeof(DesignValidationReportTool).Assembly },
            new ManifestGenerator(),
            container.Provider,
            NullLogger<ToolCatalog>.Instance);
}
