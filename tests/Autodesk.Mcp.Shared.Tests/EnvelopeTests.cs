using System.Text.Json;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Errors;
using Autodesk.Mcp.Shared.Serialization;
using Xunit;

namespace Autodesk.Mcp.Shared.Tests;

/// <summary>Wire shape of the request/response envelopes and their identifiers.</summary>
public class EnvelopeTests
{
    [Fact]
    public void RequestEnvelope_UsesCamelCaseWireNames()
    {
        var request = RequestEnvelope.Create(ProtocolConstants.ToolsExecute, 42, correlationId: "c-1", sessionId: "s-1", timeoutMilliseconds: 5000);

        string json = ProtocolSerializer.Serialize(request);

        Assert.Contains("\"method\":\"tools/execute\"", json);
        Assert.Contains("\"id\":42", json);
        Assert.Contains("\"correlationId\":\"c-1\"", json);
        Assert.Contains("\"timeoutMilliseconds\":5000", json);
        Assert.Contains("\"clientRequestedAtUtc\":", json);
    }

    [Fact]
    public void Notification_OmitsIdMember()
    {
        var notification = RequestEnvelope.Create(ProtocolConstants.ProgressNotification);

        string json = ProtocolSerializer.Serialize(notification);

        Assert.DoesNotContain("\"id\"", json);
        var back = ProtocolSerializer.Deserialize<RequestEnvelope>(json);
        Assert.Null(back?.Id);
    }

    [Fact]
    public void SuccessEnvelope_WireShape()
    {
        using var document = JsonDocument.Parse("{\"name\":\"A1\"}");
        var envelope = ResponseEnvelope.Ok(data: document.RootElement.Clone(), message: "done", correlationId: "c-1", executionTime: 12);

        string json = ProtocolSerializer.Serialize(envelope);

        Assert.Contains("\"success\":true", json);
        Assert.Contains("\"message\":\"done\"", json);
        Assert.Contains("\"executionTime\":12", json);
        Assert.Contains("\"errorCode\":\"E_UNKNOWN\"", json);
        Assert.Contains("\"data\":{\"name\":\"A1\"}", json);
        Assert.DoesNotContain("stackTrace", json);
    }

    [Fact]
    public void FailureEnvelope_WireShape()
    {
        var envelope = ResponseEnvelope.Fail(ErrorCode.E_NO_ACTIVE_DOCUMENT, "No active document.", correlationId: "c-2");

        string json = ProtocolSerializer.Serialize(envelope);

        Assert.Contains("\"success\":false", json);
        Assert.Contains("\"errorCode\":\"E_NO_ACTIVE_DOCUMENT\"", json);
        Assert.Contains("\"correlationId\":\"c-2\"", json);
    }

    [Fact]
    public void NullValues_AreOmitted()
    {
        string json = ProtocolSerializer.Serialize(ResponseEnvelope.Ok());

        Assert.DoesNotContain("\"correlationId\":null", json);
        Assert.DoesNotContain("\"data\":null", json);
    }

    [Fact]
    public void GenericEnvelope_RoundTrips()
    {
        var original = ResponseEnvelope<int>.Ok(42, message: "ok");

        var result = ProtocolSerializer.Deserialize<ResponseEnvelope<int>>(ProtocolSerializer.Serialize(original));

        Assert.True(result!.Success);
        Assert.Equal(42, result.Data);
        Assert.Equal("ok", result.Message);
    }

    [Fact]
    public void JsonRpcId_NumberAndString_RoundTrip()
    {
        Assert.Equal(7L, ProtocolSerializer.Deserialize<JsonRpcId>("7").AsNumber());
        Assert.Equal("abc", ProtocolSerializer.Deserialize<JsonRpcId>("\"abc\"").AsString());
        Assert.Equal("\"abc\"", ProtocolSerializer.Serialize(new JsonRpcId("abc")));
        Assert.Equal("5", ProtocolSerializer.Serialize(new JsonRpcId(5)));
        Assert.True(ProtocolSerializer.Deserialize<JsonRpcId>("null").IsNull);
    }

    [Fact]
    public void Version_ReadsFromStringAndObject()
    {
        Assert.Equal(new VersionInformation(1, 2, 3), ProtocolSerializer.Deserialize<VersionInformation>("\"1.2.3\""));
        Assert.Equal(
            new VersionInformation(1, 2, 3, "beta", "b1"),
            ProtocolSerializer.Deserialize<VersionInformation>("{\"major\":1,\"minor\":2,\"patch\":3,\"preRelease\":\"beta\",\"buildMetadata\":\"b1\"}"));
        Assert.Equal("\"1.2.3\"", ProtocolSerializer.Serialize(new VersionInformation(1, 2, 3)));
    }
}
