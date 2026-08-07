using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Dtos;
using Autodesk.Mcp.Shared.Enums;
using Autodesk.Mcp.Shared.Serialization;
using Xunit;

namespace Autodesk.Mcp.Shared.Tests;

/// <summary>Serialization round-trip coverage for the handshake, discovery and notification DTOs.</summary>
public class SerializationTests
{
    private static T RoundTrip<T>(T value)
        => ProtocolSerializer.Deserialize<T>(ProtocolSerializer.Serialize(value))!;

    [Fact]
    public void HandshakeRequest_RoundTrips()
    {
        var original = new HandshakeRequest
        {
            ProtocolVersion = new VersionInformation(1, 2, 3),
            ClientName = "Autodesk.MCP.Server",
            ClientVersion = "4.5.6",
            Capabilities = new ClientCapabilities { SupportsConfirmation = true, SupportsProgress = true },
        };

        var result = RoundTrip(original);

        Assert.Equal(original, result);
    }

    [Fact]
    public void HandshakeResponse_RoundTrips()
    {
        var original = new HandshakeResponse
        {
            ProtocolVersion = new VersionInformation(1, 0, 0),
            SessionId = "s-123",
            Bridge = new BridgeInformation
            {
                BridgeName = "Civil3D.Bridge",
                Product = "Civil3D",
                ProductVersion = "2026",
                BridgeVersion = new VersionInformation(1, 0, 0),
                SdkVersion = new VersionInformation(1, 0, 0),
                ProtocolVersion = new VersionInformation(1, 0, 0),
                Capabilities = new BridgeCapabilities
                {
                    SupportsProgress = true,
                    SupportsCancellation = true,
                    SupportedProtocolVersion = new VersionInformation(1, 0, 0),
                    SupportedProducts = new[] { "Civil3D", "AutoCAD" },
                },
            },
        };

        var result = RoundTrip(original);

        var bridge = original.Bridge!;
        var bridgeBack = result.Bridge!;
        Assert.Equal(original.ProtocolVersion, result.ProtocolVersion);
        Assert.Equal(original.SessionId, result.SessionId);
        Assert.Equal(bridge.BridgeName, bridgeBack.BridgeName);
        Assert.Equal(bridge.Product, bridgeBack.Product);
        Assert.Equal(bridge.Capabilities!.SupportedProtocolVersion, bridgeBack.Capabilities!.SupportedProtocolVersion);
        Assert.Equal(bridge.Capabilities.SupportedProducts, bridgeBack.Capabilities.SupportedProducts);
    }

    [Fact]
    public void EndpointDescriptor_RoundTrips()
    {
        var original = new EndpointDescriptor
        {
            BridgeName = "Civil3D.Bridge",
            Product = "Civil3D",
            ProductVersion = "2026",
            BridgeVersion = new VersionInformation(1, 0, 0),
            ProtocolVersion = new VersionInformation(1, 0, 0),
            PipeName = "autodesk-mcp-civil3d-12345",
            ProcessId = 12345,
            Capabilities = new BridgeCapabilities { SupportsProgress = true },
        };

        var result = RoundTrip(original);

        Assert.Equal(original.PipeName, result.PipeName);
        Assert.Equal(original.ProcessId, result.ProcessId);
        Assert.Equal(original.Capabilities!.SupportsProgress, result.Capabilities!.SupportsProgress);
        Assert.Equal(original.BridgeVersion, result.BridgeVersion);

        string json = ProtocolSerializer.Serialize(original);
        Assert.Contains("\"pid\":12345", json);
        Assert.Contains("\"startedUtc\":", json);
        Assert.Contains("\"pipeName\":\"autodesk-mcp-civil3d-12345\"", json);
    }

    [Fact]
    public void CorrelationInformation_RoundTrips()
    {
        var original = new CorrelationInformation
        {
            CorrelationId = "corr-1",
            ParentCorrelationId = "corr-0",
            SessionId = "s-9",
            Source = "test",
        };

        var result = RoundTrip(original);

        Assert.Equal(original, result);
    }

    [Fact]
    public void NotificationTypes_RoundTrip()
    {
        var progress = new ProgressNotification { CorrelationId = "c1", Percent = 42, ToolName = "rebuild", Stage = "meshing" };
        Assert.Equal(progress.Percent, RoundTrip(progress).Percent);
        Assert.Equal(progress.CorrelationId, RoundTrip(progress).CorrelationId);

        var cancel = new CancellationRequest { CorrelationId = "c1", Reason = "user abort" };
        Assert.Equal(cancel, RoundTrip(cancel));

        var heartbeat = new Heartbeat { ProcessId = 99, SessionId = "s" };
        Assert.Equal(heartbeat.ProcessId, RoundTrip(heartbeat).ProcessId);

        var confirm = new ConfirmationRequest { RequestId = "r1", Title = "Delete layer", Message = "Really?", Risk = ToolRisk.High };
        var confirmBack = RoundTrip(confirm);
        Assert.Equal(confirm.RequestId, confirmBack.RequestId);
        Assert.Equal(confirm.Risk, confirmBack.Risk);

        var confirmAnswer = new ConfirmationResponse { RequestId = "r1", Confirmed = true };
        Assert.Equal(confirmAnswer, RoundTrip(confirmAnswer));
    }
}
