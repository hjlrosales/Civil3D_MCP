using Autodesk.Mcp.Sdk.Dispatch;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Hosting;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Dtos;
using Civil3D.Bridge.Execution;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Drawing.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Civil3D.Tools.Drawing.Tests;

/// <summary>Shared test doubles for the drawing tool tests.</summary>
internal static class TestDoubles
{
    /// <summary>A session returning a canned snapshot (or null for the no-document case).</summary>
    internal sealed class FakeSession : ICivil3DSession
    {
        private readonly ActiveDrawing? _drawing;

        public FakeSession(ActiveDrawing? drawing) => _drawing = drawing;

        public ActiveDrawing? GetActiveDrawing() => _drawing;
    }

    /// <summary>A statistics service returning a canned result or delegating to a factory.</summary>
    internal sealed class FakeStatisticsService : IDrawingStatisticsService
    {
        private readonly Func<ActiveDrawing, DrawingStatistics> _factory;

        public FakeStatisticsService(DrawingStatistics statistics)
            : this(_ => statistics)
        {
        }

        public FakeStatisticsService(Func<ActiveDrawing, DrawingStatistics> factory) => _factory = factory;

        public DrawingStatistics GetStatistics(ActiveDrawing drawing, CancellationToken cancellationToken)
            => _factory(drawing);
    }

    /// <summary>A fixed bridge identity provider.</summary>
    internal sealed class TestInfoProvider : IEndpointInfoProvider
    {
        public BridgeInformation GetBridgeInformation() => new()
        {
            BridgeName = "Test.Bridge",
            Product = "Civil3D",
            ProductVersion = "2025",
            BridgeVersion = new VersionInformation(1, 2, 3),
            SdkVersion = new VersionInformation(4, 5, 6),
            ProtocolVersion = ProtocolConstants.CurrentProtocolVersion,
            Capabilities = new BridgeCapabilities { SupportsCancellation = true },
        };

        public EndpointDescriptor CreateEndpointDescriptor() => new()
        {
            BridgeName = "Test.Bridge",
            Product = "Civil3D",
            PipeName = "pipe",
            ProtocolVersion = ProtocolConstants.CurrentProtocolVersion,
        };
    }

    /// <summary>Runs the action inline, mimicking the application-context marshaler.</summary>
    internal sealed class InlineContext : IApplicationContext
    {
        public Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken) => action();
    }

    /// <summary>Builds a started <see cref="ToolDispatcher"/> over the given catalog.</summary>
    internal static ToolDispatcher CreateDispatcher(IToolCatalog catalog)
    {
        var dispatcher = new ToolDispatcher(catalog, new InlineContext(), new CancellationRegistry(), NullLogger<ToolDispatcher>.Instance);
        dispatcher.Start();
        return dispatcher;
    }

    /// <summary>A standard invocation for dispatcher-level tests.</summary>
    internal static ToolInvocation Invoke(string tool) => new()
    {
        ToolName = tool,
        CorrelationId = "c-1",
        TimeoutMilliseconds = 5_000,
    };

    /// <summary>Executes a tool directly with a fixed context (no dispatcher).</summary>
    internal static Task<object?> ExecuteAsync(ITool tool)
    {
        var context = new ToolExecutionContext
        {
            ToolName = tool.Name,
            CorrelationId = "c-1",
            SessionId = "s-1",
            CancellationToken = CancellationToken.None,
        };
        return tool.ExecuteAsync(context, null);
    }

    /// <summary>Builds a real <see cref="ToolCatalog"/> over the Drawing assembly with fake services.</summary>
    internal static ToolCatalog CreateCatalog(
        ICivil3DSession? session = null,
        IDrawingStatisticsService? statistics = null,
        IEndpointInfoProvider? info = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(session ?? new FakeSession(SampleDrawing()));
        services.AddSingleton(statistics ?? new FakeStatisticsService(SampleStatistics()));
        services.AddSingleton(info ?? new TestInfoProvider());

        return new ToolCatalog(
            new[] { typeof(DrawingInfoTool).Assembly },
            new ManifestGenerator(),
            services.BuildServiceProvider(),
            NullLogger<ToolCatalog>.Instance);
    }

    /// <summary>A representative active drawing snapshot.</summary>
    internal static ActiveDrawing SampleDrawing() => new()
    {
        DrawingName = "Sample.dwg",
        DrawingPath = @"C:\Drawings\Sample.dwg",
        DrawingVersion = "AC1032",
        IsModified = true,
        IsReadOnly = false,
        CurrentLayout = "Model",
        IsModelSpaceActive = true,
        DatabaseFingerprint = "fp-123",
        Civil3DVersion = "25.0",
        OpenDocumentsCount = 2,
        CurrentDocumentName = "Sample.dwg",
        CurrentDocumentPath = @"C:\Drawings\Sample.dwg",
    };

    /// <summary>A representative statistics payload.</summary>
    internal static DrawingStatistics SampleStatistics() => new()
    {
        LayerCount = 42,
        BlockCount = 13,
        XRefCount = 2,
        EntityCount = 3_000,
        ModelSpaceEntityCount = 2_900,
        PaperSpaceEntityCount = 100,
        ViewportCount = 4,
        TextStyleCount = 7,
        DimensionStyleCount = 3,
        LinetypeCount = 5,
        RegisteredApplicationCount = 6,
        DictionaryCount = 9,
        ApproximateDrawingSizeBytes = 12_345_678,
    };
}
