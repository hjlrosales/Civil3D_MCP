using Autodesk.Mcp.Sdk.Discovery;
using Civil3D.Domain.Commands;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static Civil3D.Tools.Workflows.Tests.TestDoubles;
using static Civil3D.Tools.Workflows.Tests.TestTools;

namespace Civil3D.Tools.Workflows.Tests;

/// <summary>
/// Builds the real workflow framework (WorkflowDispatcher) over the in-memory store, registers
/// the test handlers/validators, and exposes a <see cref="ToolCatalog"/> over the test tools.
/// </summary>
internal static class WorkflowToolHarness
{
    internal sealed record Container(
        ServiceProvider Provider,
        InMemoryDomainEventDispatcher Events,
        FakeStore Store);

    internal static Container CreateContainer(ICivil3DSession? session = null)
    {
        var services = new ServiceCollection();
        var events = new InMemoryDomainEventDispatcher();
        var store = new FakeStore();

        services.AddSingleton<IDomainEventDispatcher>(events);
        services.AddSingleton(store);
        services.AddSingleton<IWorkflowDispatcher>(sp => new WorkflowDispatcher(
            sp,
            sp.GetRequiredService<IDomainEventDispatcher>(),
            NullLogger<WorkflowDispatcher>.Instance));
        services.AddSingleton<ICivil3DSession>(session ?? new FakeSession(SampleDrawing()));

        services.AddTransient<IWorkflowHandler<ReportWorkflow, ReportResult>, GenericHandler<ReportWorkflow>>();
        services.AddTransient<IWorkflowHandler<DeniedWorkflow, ReportResult>, GenericHandler<DeniedWorkflow>>();
        services.AddTransient<IWorkflowHandler<TimeoutWorkflow, ReportResult>, GenericHandler<TimeoutWorkflow>>();
        services.AddTransient<IWorkflowHandler<FailingWorkflow, ReportResult>, GenericHandler<FailingWorkflow>>();
        services.AddTransient<IWorkflowHandler<DomainFailWorkflow, ReportResult>, GenericHandler<DomainFailWorkflow>>();
        services.AddTransient<IWorkflowValidator<ReportWorkflow>, ValueRequiredValidator>();

        ServiceProvider provider = services.BuildServiceProvider();
        return new Container(provider, events, store);
    }

    internal static ToolCatalog CreateCatalog(Container container)
        => new(
            new[] { typeof(ReportWorkflowTool).Assembly },
            new ManifestGenerator(),
            container.Provider,
            NullLogger<ToolCatalog>.Instance);
}
