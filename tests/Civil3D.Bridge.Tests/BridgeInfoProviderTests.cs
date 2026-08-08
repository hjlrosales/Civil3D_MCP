using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Dtos;
using Civil3D.Bridge.Configuration;
using Civil3D.Bridge.Services;
using Xunit;

namespace Civil3D.Bridge.Tests;

/// <summary>Bridge identity: handshake payload and endpoint descriptor.</summary>
public class BridgeInfoProviderTests
{
    private static BridgeOptions CreateOptions() => new()
    {
        BridgeName = "Civil3D.Bridge",
        Product = "Civil3D",
        ProductVersion = "2025",
        BridgeVersion = "1.0.0",
        SdkVersion = "1.0.0",
        PipeName = "autodesk-mcp-civil3d-1234",
        SupportedProducts = new[] { "Civil3D" },
        SupportsCancellation = true,
    };

    [Fact]
    public void GetBridgeInformation_ReturnsIdentity()
    {
        var provider = new BridgeInfoProvider(CreateOptions());

        BridgeInformation info = provider.GetBridgeInformation();

        Assert.Equal("Civil3D.Bridge", info.BridgeName);
        Assert.Equal("Civil3D", info.Product);
        Assert.Equal("2025", info.ProductVersion);
        Assert.Equal(new VersionInformation(1, 0, 0), info.BridgeVersion);
        Assert.Equal(ProtocolConstants.CurrentProtocolVersion, info.ProtocolVersion);
        Assert.NotNull(info.Capabilities);
        Assert.True(info.Capabilities!.SupportsCancellation);
    }

    [Fact]
    public void CreateEndpointDescriptor_HasPidPipeAndStartedUtc()
    {
        var provider = new BridgeInfoProvider(CreateOptions());

        EndpointDescriptor descriptor = provider.CreateEndpointDescriptor();

        Assert.Equal("autodesk-mcp-civil3d-1234", descriptor.PipeName);
        Assert.Equal(Environment.ProcessId, descriptor.ProcessId);
        Assert.Equal("Civil3D", descriptor.Product);
        Assert.True((DateTimeOffset.UtcNow - descriptor.StartedAtUtc).Duration() < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void InvalidVersions_FallBackToDefaults()
    {
        BridgeOptions options = CreateOptions();
        options.BridgeVersion = "not-a-version";
        var provider = new BridgeInfoProvider(options);

        BridgeInformation info = provider.GetBridgeInformation();

        Assert.Equal(new VersionInformation(1, 0, 0), info.BridgeVersion);
    }
}
