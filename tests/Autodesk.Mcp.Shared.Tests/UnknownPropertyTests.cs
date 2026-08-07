using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Errors;
using Autodesk.Mcp.Shared.Serialization;
using Xunit;

namespace Autodesk.Mcp.Shared.Tests;

/// <summary>
/// Version tolerance: payloads from newer or older peers must never crash deserialization.
/// </summary>
public class UnknownPropertyTests
{
    [Fact]
    public void ExtraProperties_AreSkipped()
    {
        const string json = "{\"success\":true,\"message\":\"ok\",\"executionTime\":10,\"errorCode\":\"E_UNKNOWN\",\"futureField\":\"x\",\"nested\":{\"a\":1}}";

        var envelope = ProtocolSerializer.Deserialize<ResponseEnvelope>(json);

        Assert.True(envelope!.Success);
        Assert.Equal(10, envelope.ExecutionTime);
        Assert.Equal("ok", envelope.Message);
    }

    [Fact]
    public void UnknownEnumValue_FallsBackToEUnknown()
    {
        const string json = "{\"success\":false,\"errorCode\":\"E_FUTURE_CODE\",\"message\":\"x\"}";

        var envelope = ProtocolSerializer.Deserialize<ResponseEnvelope>(json);

        Assert.Equal(ErrorCode.E_UNKNOWN, envelope!.ErrorCode);
    }

    [Fact]
    public void Numbers_ReadFromStrings()
    {
        const string json = "{\"success\":true,\"executionTime\":\"42\",\"message\":\"\"}";

        var envelope = ProtocolSerializer.Deserialize<ResponseEnvelope>(json);

        Assert.Equal(42L, envelope!.ExecutionTime);
    }

    [Fact]
    public void PropertyNames_AreCaseInsensitive()
    {
        const string json = "{\"SUCCESS\":true,\"MESSAGE\":\"hi\",\"EXECUTIONTIME\":1,\"ERRORCODE\":\"E_TIMEOUT\"}";

        var envelope = ProtocolSerializer.Deserialize<ResponseEnvelope>(json);

        Assert.True(envelope!.Success);
        Assert.Equal(ErrorCode.E_TIMEOUT, envelope.ErrorCode);
        Assert.Equal("hi", envelope.Message);
    }

    [Fact]
    public void TrailingCommas_AreTolerated()
    {
        const string json = "{\"success\":true,\"message\":\"ok\",}";

        var envelope = ProtocolSerializer.Deserialize<ResponseEnvelope>(json);

        Assert.True(envelope!.Success);
    }
}
