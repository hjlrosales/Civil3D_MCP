using System.Text.Json;
using Autodesk.Mcp.Sdk.Dispatch;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Hosting;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Dtos;
using Autodesk.Mcp.Shared.Errors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Autodesk.Mcp.Sdk.Tests;

/// <summary>JSON-RPC routing, error mapping and cancellation notifications.</summary>
public class RouterTests
{
    private static (JsonRpcRouter Router, SessionStore Sessions) CreateRouter(IToolCatalog? catalog = null, IToolExecutor? executor = null)
    {
        var sessions = new SessionStore();
        var cancellations = new CancellationRegistry();
        catalog ??= new ToolCatalog(
            new[] { typeof(EchoTool).Assembly },
            new ManifestGenerator(),
            new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider(),
            NullLogger<ToolCatalog>.Instance);
        executor ??= new ImmediateExecutor();
        var info = new TestInfoProvider();

        var router = new JsonRpcRouter(
            new IProtocolHandler[]
            {
                new HandshakeHandler(info, sessions, NullLogger<HandshakeHandler>.Instance),
                new ListToolsHandler(catalog),
                new ExecuteToolHandler(catalog, executor, NullLogger<ExecuteToolHandler>.Instance),
                new PingHandler(),
                new ShutdownHandler(new BridgeShutdown()),
            },
            cancellations,
            NullLogger<JsonRpcRouter>.Instance);

        return (router, sessions);
    }

    [Fact]
    public async Task Handshake_CreatesSession_AndReturnsBridgeInfo()
    {
        (JsonRpcRouter router, SessionStore sessions) = CreateRouter();
        var request = new RequestEnvelope
        {
            Method = ProtocolConstants.Handshake,
            Id = new JsonRpcId(1),
            Params = JsonDocument.Parse("{\"protocolVersion\":\"1.0.0\",\"clientName\":\"test-client\"}").RootElement,
        };

        ResponseEnvelope response = (await router.HandleAsync(request, CancellationToken.None))!;

        Assert.True(response.Success);
        Assert.Single(sessions.All);
    }

    [Fact]
    public async Task UnknownMethod_ReturnsInvalidRequest()
    {
        (JsonRpcRouter router, _) = CreateRouter();
        var request = new RequestEnvelope { Method = "nope", Id = new JsonRpcId(1) };

        ResponseEnvelope response = (await router.HandleAsync(request, CancellationToken.None))!;

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.E_INVALID_REQUEST, response.ErrorCode);
    }

    [Fact]
    public async Task ToolsList_ReturnsManifest()
    {
        (JsonRpcRouter router, _) = CreateRouter();
        var request = new RequestEnvelope { Method = ProtocolConstants.ToolsList, Id = new JsonRpcId(1) };

        ResponseEnvelope response = (await router.HandleAsync(request, CancellationToken.None))!;

        Assert.True(response.Success);
        Manifest? manifest = response.Data!.Value.Deserialize<Manifest>(Autodesk.Mcp.Shared.Serialization.SharedJson.Options);
        Assert.NotNull(manifest);
        Assert.Contains(manifest!.Tools, static t => t.Name == "test.echo");
    }

    [Fact]
    public async Task Execute_ReturnsEnvelope()
    {
        (JsonRpcRouter router, _) = CreateRouter();
        var request = new RequestEnvelope
        {
            Method = ProtocolConstants.ToolsExecute,
            Id = new JsonRpcId(2),
            Params = JsonDocument.Parse("{\"tool\":\"test.echo\",\"arguments\":{\"text\":\"hi\"}}").RootElement,
        };

        ResponseEnvelope response = (await router.HandleAsync(request, CancellationToken.None))!;

        Assert.True(response.Success);
    }

    [Fact]
    public async Task Execute_UnknownTool_MapsToObjectNotFound()
    {
        (JsonRpcRouter router, _) = CreateRouter();
        var request = new RequestEnvelope
        {
            Method = ProtocolConstants.ToolsExecute,
            Id = new JsonRpcId(3),
            Params = JsonDocument.Parse("{\"tool\":\"missing\"}").RootElement,
        };

        ResponseEnvelope response = (await router.HandleAsync(request, CancellationToken.None))!;

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.E_OBJECT_NOT_FOUND, response.ErrorCode);
    }

    [Fact]
    public async Task CancelNotification_ReturnsNoResponse()
    {
        (JsonRpcRouter router, _) = CreateRouter();
        var request = new RequestEnvelope
        {
            Method = ProtocolConstants.CancelNotification,
            Params = JsonDocument.Parse("{\"correlationId\":\"c-1\"}").RootElement,
        };

        ResponseEnvelope? response = await router.HandleAsync(request, CancellationToken.None);

        Assert.Null(response);
    }

    [Fact]
    public async Task HandlerException_MapsToStableError()
    {
        (JsonRpcRouter router, _) = CreateRouter(executor: new ThrowingExecutor());
        var request = new RequestEnvelope
        {
            Method = ProtocolConstants.ToolsExecute,
            Id = new JsonRpcId(4),
            Params = JsonDocument.Parse("{\"tool\":\"test.echo\"}").RootElement,
        };

        ResponseEnvelope response = (await router.HandleAsync(request, CancellationToken.None))!;

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.E_INTERNAL, response.ErrorCode);
    }

    private sealed class ImmediateExecutor : IToolExecutor
    {
        public Task<ResponseEnvelope> ExecuteAsync(ToolInvocation invocation, CancellationToken cancellationToken)
            => Task.FromResult(ResponseEnvelope.Ok(correlationId: invocation.CorrelationId));
    }

    private sealed class ThrowingExecutor : IToolExecutor
    {
        public Task<ResponseEnvelope> ExecuteAsync(ToolInvocation invocation, CancellationToken cancellationToken)
            => throw new InvalidOperationException("boom");
    }

    private sealed class TestInfoProvider : Hosting.IEndpointInfoProvider
    {
        public BridgeInformation GetBridgeInformation() => new() { BridgeName = "Test", Product = "Test", ProtocolVersion = ProtocolConstants.CurrentProtocolVersion };

        public EndpointDescriptor CreateEndpointDescriptor() => new() { BridgeName = "Test", Product = "Test", PipeName = "pipe", ProtocolVersion = ProtocolConstants.CurrentProtocolVersion };
    }
}
