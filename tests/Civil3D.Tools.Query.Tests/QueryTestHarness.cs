using System.Text.Json;
using Autodesk.Mcp.Sdk.Dispatch;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Hosting;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Bridge.Execution;
using Civil3D.Domain.Alignments.Services;
using Civil3D.Domain.Cogo.Services;
using Civil3D.Domain.Corridors.Services;
using Civil3D.Domain.Pipes.Services;
using Civil3D.Domain.Profiles.Services;
using Civil3D.Domain.Styles.Services;
using Civil3D.Domain.Surfaces.Services;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Query.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Civil3D.Tools.Query.Tests;

/// <summary>
/// Shared harness for the query tool tests: a real <see cref="ToolCatalog"/> over the
/// Civil3D.Tools.Query assembly with in-memory fake services, a started
/// <see cref="ToolDispatcher"/>, and a JSON parameter helper. Query behavior flows through the
/// real <see cref="Civil3D.Domain.Query.QueryEngine"/> exactly as in production.
/// </summary>
internal static class QueryTestHarness
{
    internal static ToolCatalog CreateCatalog(ICivil3DSession? session = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICivil3DSession>(session ?? new FakeServices.FakeSession(SampleData.Drawing));
        services.AddSingleton<IAlignmentService>(new FakeServices.FakeAlignmentService(SampleData.Alignments));
        services.AddSingleton<ISurfaceService>(new FakeServices.FakeSurfaceService(SampleData.Surfaces));
        services.AddSingleton<IProfileService>(new FakeServices.FakeProfileService(SampleData.Profiles));
        services.AddSingleton<ICorridorService>(new FakeServices.FakeCorridorService(SampleData.Corridors));
        services.AddSingleton<IPipeService>(new FakeServices.FakePipeService(SampleData.PipeNetworks));
        services.AddSingleton<ICogoService>(new FakeServices.FakeCogoService(SampleData.CogoPoints));
        services.AddSingleton<IStyleService>(new FakeServices.FakeStyleService(SampleData.Styles));

        return new ToolCatalog(
            new[] { typeof(ListAlignmentsTool).Assembly },
            new ManifestGenerator(),
            services.BuildServiceProvider(),
            NullLogger<ToolCatalog>.Instance);
    }

    internal static ToolDispatcher CreateDispatcher(ToolCatalog catalog)
    {
        var dispatcher = new ToolDispatcher(
            catalog,
            new InlineContext(),
            new CancellationRegistry(),
            NullLogger<ToolDispatcher>.Instance);
        dispatcher.Start();
        return dispatcher;
    }

    /// <summary>Runs the action inline, mimicking the application-context marshaler.</summary>
    internal sealed class InlineContext : IApplicationContext
    {
        public Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken) => action();
    }

    /// <summary>Builds an invocation with raw JSON parameters serialized with the shared options.</summary>
    internal static ToolInvocation Invoke(string tool, object? parameters = null) => new()
    {
        ToolName = tool,
        Parameters = parameters is null ? null : JsonSerializer.SerializeToElement(parameters, SharedJson.Options),
        CorrelationId = "c-query",
        SessionId = "s-query",
        TimeoutMilliseconds = 10_000,
    };
}
