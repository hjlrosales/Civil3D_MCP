using Autodesk.Mcp.Sdk.Dispatch;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Errors;
using Civil3D.Bridge.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Civil3D.Bridge.Tests;

/// <summary>Dispatcher behavior: FIFO ordering, marshaling, cancellation, timeouts, error mapping.</summary>
public class ToolDispatcherTests
{
    private sealed class FakeCatalog : IToolCatalog
    {
        private readonly Dictionary<string, ITool> _tools;
        public IReadOnlyList<Autodesk.Mcp.Shared.Dtos.ToolManifest> Manifests { get; }
        public IReadOnlyCollection<string> ToolNames => _tools.Keys.ToArray();

        public FakeCatalog(params ITool[] tools)
        {
            _tools = tools.ToDictionary(t => t.Name);
            Manifests = tools.Select(t => new Autodesk.Mcp.Shared.Dtos.ToolManifest
            {
                Name = t.Name,
                DisplayName = t.Name,
                Description = "test",
                TimeoutMilliseconds = 10_000,
                InputSchema = Autodesk.Mcp.Shared.Schemas.JsonSchemaDocument.Empty,
                OutputSchema = Autodesk.Mcp.Shared.Schemas.JsonSchemaDocument.Empty,
            }).ToArray();
        }

        public bool TryGetTool(string name, out ITool tool)
        {
            if (_tools.TryGetValue(name, out ITool? found))
            {
                tool = found!;
                return true;
            }

            tool = null!;
            return false;
        }
        public Autodesk.Mcp.Shared.Dtos.ToolManifest? GetManifest(string name) => Manifests.FirstOrDefault(m => m.Name == name);
        public NJsonSchema.JsonSchema? GetInputSchema(string name) => null;
    }

    private sealed class InlineContext : IApplicationContext
    {
        private int _invocations;
        public int Invocations => _invocations;

        public Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _invocations);
            return action();
        }
    }

    private static ToolDispatcher CreateDispatcher(FakeCatalog catalog, InlineContext context)
    {
        var dispatcher = new ToolDispatcher(catalog, context, new CancellationRegistry(), NullLogger<ToolDispatcher>.Instance);
        dispatcher.Start();
        return dispatcher;
    }

    private static ToolInvocation Invoke(string tool, string? correlation = null)
        => new() { ToolName = tool, CorrelationId = correlation, TimeoutMilliseconds = 5_000 };

    [Fact]
    public async Task Echo_RunsAndReturnsData()
    {
        var catalog = new FakeCatalog(new EchoBridgeTool());
        var dispatcher = CreateDispatcher(catalog, new InlineContext());

        ResponseEnvelope response = await dispatcher.ExecuteAsync(Invoke("echo", "c-1"), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("c-1", response.CorrelationId);
    }

    [Fact]
    public async Task UnknownTool_ReturnsObjectNotFound()
    {
        var catalog = new FakeCatalog();
        var dispatcher = CreateDispatcher(catalog, new InlineContext());

        ResponseEnvelope response = await dispatcher.ExecuteAsync(Invoke("missing"), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.E_OBJECT_NOT_FOUND, response.ErrorCode);
    }

    [Fact]
    public async Task BridgeException_MapsToErrorCode()
    {
        var catalog = new FakeCatalog(new ThrowingBridgeTool());
        var dispatcher = CreateDispatcher(catalog, new InlineContext());

        ResponseEnvelope response = await dispatcher.ExecuteAsync(Invoke("throw-bridge"), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.E_TRANSACTION_FAILED, response.ErrorCode);
    }

    [Fact]
    public async Task GenericException_IsNeverExposed()
    {
        var catalog = new FakeCatalog(new ThrowingGenericTool());
        var dispatcher = CreateDispatcher(catalog, new InlineContext());

        ResponseEnvelope response = await dispatcher.ExecuteAsync(Invoke("throw-generic"), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.E_INTERNAL, response.ErrorCode);
        Assert.DoesNotContain("boom", response.Message);
    }

    [Fact]
    public async Task ContextTool_IsMarshaledThroughApplicationContext()
    {
        var context = new InlineContext();
        var catalog = new FakeCatalog(new ContextTool());
        var dispatcher = CreateDispatcher(catalog, context);

        await dispatcher.ExecuteAsync(Invoke("context-tool"), CancellationToken.None);

        Assert.Equal(1, context.Invocations);
    }

    [Fact]
    public async Task Cancellation_ReturnsCancelled()
    {
        var catalog = new FakeCatalog(new SlowBridgeTool());
        var dispatcher = CreateDispatcher(catalog, new InlineContext());
        using var cts = new CancellationTokenSource(50);

        ResponseEnvelope response = await dispatcher.ExecuteAsync(Invoke("slow"), cts.Token);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.E_CANCELLED, response.ErrorCode);
    }

    [Fact]
    public async Task RegistryCancel_AbortsInFlightTool()
    {
        var cancellations = new CancellationRegistry();
        var catalog = new FakeCatalog(new SlowBridgeTool());
        var dispatcher = new ToolDispatcher(catalog, new InlineContext(), cancellations, NullLogger<ToolDispatcher>.Instance);
        dispatcher.Start();

        var invocation = new ToolInvocation { ToolName = "slow", CorrelationId = "c-1", TimeoutMilliseconds = 60_000 };
        Task<ResponseEnvelope> running = dispatcher.ExecuteAsync(invocation, CancellationToken.None);
        await Task.Delay(50); // let the tool start
        cancellations.Cancel("c-1");

        ResponseEnvelope response = await running;
        Assert.False(response.Success);
        Assert.Equal(ErrorCode.E_CANCELLED, response.ErrorCode);
    }

    [Fact]
    public async Task Timeout_ReturnsTimeout()
    {
        var catalog = new FakeCatalog(new SlowBridgeTool());
        var dispatcher = CreateDispatcher(catalog, new InlineContext());

        var invocation = new ToolInvocation { ToolName = "slow", TimeoutMilliseconds = 50 };
        ResponseEnvelope response = await dispatcher.ExecuteAsync(invocation, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.E_TIMEOUT, response.ErrorCode);
    }

    [Fact]
    public async Task Fifo_ExecutesInOrder()
    {
        var order = new List<string>();
        var tool = new OrderTool(order);
        var catalog = new FakeCatalog(tool);
        var dispatcher = CreateDispatcher(catalog, new InlineContext());

        Task<ResponseEnvelope> first = dispatcher.ExecuteAsync(Invoke("order", "a"), CancellationToken.None);
        Task<ResponseEnvelope> second = dispatcher.ExecuteAsync(Invoke("order", "b"), CancellationToken.None);
        await Task.WhenAll(first, second);

        Assert.Equal(new[] { "a", "b" }, order);
    }

    // --- Test tools ---

    [Autodesk.Mcp.Sdk.Tools.McpTool("echo", "Echo", "test")]
    private sealed class EchoBridgeTool : ToolBase<EmptyParameters, EchoResult>
    {
        protected override Task<EchoResult> ExecuteCoreAsync(EmptyParameters input, ToolExecutionContext context, CancellationToken cancellationToken)
            => Task.FromResult(new EchoResult { CorrelationId = context.CorrelationId });
    }

    private sealed class EchoResult { public string? CorrelationId { get; set; } }

    [Autodesk.Mcp.Sdk.Tools.McpTool("throw-bridge", "Throw Bridge", "test")]
    private sealed class ThrowingBridgeTool : ToolBase<EmptyParameters, EchoResult>
    {
        protected override Task<EchoResult> ExecuteCoreAsync(EmptyParameters input, ToolExecutionContext context, CancellationToken cancellationToken)
            => throw new BridgeException(ErrorCode.E_TRANSACTION_FAILED, "transaction failed");
    }

    [Autodesk.Mcp.Sdk.Tools.McpTool("throw-generic", "Throw Generic", "test")]
    private sealed class ThrowingGenericTool : ToolBase<EmptyParameters, EchoResult>
    {
        protected override Task<EchoResult> ExecuteCoreAsync(EmptyParameters input, ToolExecutionContext context, CancellationToken cancellationToken)
            => throw new InvalidOperationException("boom");
    }

    [Autodesk.Mcp.Sdk.Tools.McpTool("context-tool", "Context Tool", "test")]
    private sealed class ContextTool : ToolBase<EmptyParameters, EchoResult>
    {
        public override bool RequiresApplicationContext => true;

        protected override Task<EchoResult> ExecuteCoreAsync(EmptyParameters input, ToolExecutionContext context, CancellationToken cancellationToken)
            => Task.FromResult(new EchoResult());
    }

    [Autodesk.Mcp.Sdk.Tools.McpTool("slow", "Slow", "test")]
    private sealed class SlowBridgeTool : ToolBase<EmptyParameters, EchoResult>
    {
        protected override async Task<EchoResult> ExecuteCoreAsync(EmptyParameters input, ToolExecutionContext context, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new EchoResult();
        }
    }

    [Autodesk.Mcp.Sdk.Tools.McpTool("order", "Order", "test")]
    private sealed class OrderTool : ToolBase<EmptyParameters, EchoResult>
    {
        private readonly List<string> _order;

        public OrderTool(List<string> order) => _order = order;

        protected override Task<EchoResult> ExecuteCoreAsync(EmptyParameters input, ToolExecutionContext context, CancellationToken cancellationToken)
        {
            lock (_order)
            {
                _order.Add(context.CorrelationId);
            }

            return Task.FromResult(new EchoResult());
        }
    }
}
