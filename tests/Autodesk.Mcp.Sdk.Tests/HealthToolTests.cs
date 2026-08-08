using System.Text.Json;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Hosting;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Sdk.Tools.Health;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Autodesk.Mcp.Sdk.Tests;

/// <summary>Health tools: bridge/ping, bridge/version and bridge/getCapabilities.</summary>
public class HealthToolTests
{
    private sealed class TestInfoProvider : IEndpointInfoProvider
    {
        public BridgeInformation GetBridgeInformation() => new()
        {
            BridgeName = "Test.Bridge",
            Product = "Test",
            ProductVersion = "1.0",
            BridgeVersion = new(1, 0, 0),
            SdkVersion = new(1, 0, 0),
            ProtocolVersion = ProtocolConstants.CurrentProtocolVersion,
            Capabilities = new BridgeCapabilities { SupportsCancellation = true },
        };

        public EndpointDescriptor CreateEndpointDescriptor() => new() { BridgeName = "Test.Bridge", Product = "Test", PipeName = "pipe", ProtocolVersion = ProtocolConstants.CurrentProtocolVersion };
    }

    private static ToolCatalog CreateCatalog()
        => new(
            new[] { typeof(BridgePingTool).Assembly },
            new ManifestGenerator(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<ToolCatalog>.Instance);

    [Fact]
    public async Task Ping_ReturnsHeartbeat()
    {
        var tool = new BridgePingTool();
        var context = new ToolExecutionContext { ToolName = "bridge/ping", CorrelationId = "c-1", SessionId = "s-1", CancellationToken = CancellationToken.None };

        var result = await tool.ExecuteAsync(context, null) as Heartbeat;

        Assert.NotNull(result);
        Assert.Equal("s-1", result!.SessionId);
        Assert.True((DateTimeOffset.UtcNow - result.TimestampUtc).Duration() < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Version_ReturnsBridgeInformation()
    {
        var tool = new BridgeVersionTool(new TestInfoProvider());
        var context = new ToolExecutionContext { ToolName = "bridge/version", CorrelationId = "c-1", CancellationToken = CancellationToken.None };

        var result = await tool.ExecuteAsync(context, null) as BridgeInformation;

        Assert.NotNull(result);
        Assert.Equal("Test.Bridge", result!.BridgeName);
        Assert.Equal("Test", result.Product);
    }

    [Fact]
    public async Task GetCapabilities_ReturnsBridgeToolsAndCapabilities()
    {
        ToolCatalog catalog = CreateCatalog();
        var tool = new BridgeGetCapabilitiesTool(new TestInfoProvider(), catalog);
        var context = new ToolExecutionContext { ToolName = "bridge/getCapabilities", CorrelationId = "c-1", CancellationToken = CancellationToken.None };

        var result = await tool.ExecuteAsync(context, null) as GetCapabilitiesResult;

        Assert.NotNull(result);
        Assert.Equal("Test.Bridge", result!.Bridge.BridgeName);
        Assert.Equal(ProtocolConstants.CurrentProtocolVersion, result.ProtocolVersion);
        Assert.True(result.Capabilities.SupportsCancellation);
        Assert.Contains(result.Tools, static t => t.Name == "bridge/ping");
        Assert.Contains(result.Tools, static t => t.Name == "bridge/version");
        Assert.Contains(result.Tools, static t => t.Name == "bridge/getCapabilities");
    }

    [Fact]
    public void HealthTools_AreDiscoverableViaScanner()
    {
        IReadOnlyList<Type> types = ToolScanner.FindToolTypes(new[] { typeof(BridgePingTool).Assembly });

        Assert.Contains(types, static t => t == typeof(BridgePingTool));
        Assert.Contains(types, static t => t == typeof(BridgeVersionTool));
        Assert.Contains(types, static t => t == typeof(BridgeGetCapabilitiesTool));
    }

    [Fact]
    public async Task GetCapabilities_SerializesCleanly()
    {
        ToolCatalog catalog = CreateCatalog();
        var tool = new BridgeGetCapabilitiesTool(new TestInfoProvider(), catalog);
        var context = new ToolExecutionContext { ToolName = "bridge/getCapabilities", CorrelationId = "c-1", CancellationToken = CancellationToken.None };

        object? result = await tool.ExecuteAsync(context, null);
        string json = JsonSerializer.Serialize(result, Autodesk.Mcp.Shared.Serialization.SharedJson.Options);

        Assert.Contains("\"bridge\"", json);
        Assert.Contains("\"tools\"", json);
    }
}
