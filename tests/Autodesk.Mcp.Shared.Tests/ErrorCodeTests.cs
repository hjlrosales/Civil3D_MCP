using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Errors;
using Autodesk.Mcp.Shared.Extensions;
using Autodesk.Mcp.Shared.Serialization;
using Xunit;

namespace Autodesk.Mcp.Shared.Tests;

/// <summary>Error-code wire mapping and the exception hierarchy.</summary>
public class ErrorCodeTests
{
    [Fact]
    public void ToWireString_ReturnsMemberName()
        => Assert.Equal("E_TIMEOUT", ErrorCode.E_TIMEOUT.ToWireString());

    [Theory]
    [InlineData("E_TIMEOUT", ErrorCode.E_TIMEOUT)]
    [InlineData("e_timeout", ErrorCode.E_TIMEOUT)]
    [InlineData("E_BRIDGE_UNAVAILABLE", ErrorCode.E_BRIDGE_UNAVAILABLE)]
    public void FromWireString_IsCaseInsensitive(string wire, ErrorCode expected)
        => Assert.Equal(expected, ErrorCodeExtensions.FromWireString(wire));

    [Fact]
    public void FromWireString_UnknownMapsToEUnknown()
        => Assert.Equal(ErrorCode.E_UNKNOWN, ErrorCodeExtensions.FromWireString("E_FUTURE_CODE"));

    [Fact]
    public void TryFromWireString_FailsForJunk()
    {
        Assert.False(ErrorCodeExtensions.TryFromWireString("not a code", out ErrorCode code));
        Assert.Equal(ErrorCode.E_UNKNOWN, code);
    }

    [Fact]
    public void RoundTrip_ViaSerializer()
    {
        var envelope = ResponseEnvelope.Fail(ErrorCode.E_OBJECT_NOT_FOUND, "Missing.");

        var back = ProtocolSerializer.Deserialize<ResponseEnvelope>(ProtocolSerializer.Serialize(envelope));

        Assert.Equal(ErrorCode.E_OBJECT_NOT_FOUND, back!.ErrorCode);
    }

    [Fact]
    public void BridgeException_CarriesCodeAndContext()
    {
        var ex = new BridgeException(ErrorCode.E_NO_ACTIVE_DOCUMENT, "No doc", "c-1", "s-1");

        Assert.Equal(ErrorCode.E_NO_ACTIVE_DOCUMENT, ex.ErrorCode);
        Assert.Equal("c-1", ex.CorrelationId);
        Assert.Equal("s-1", ex.SessionId);

        var protocol = new ProtocolException("bad rpc", "c-2");
        Assert.Equal(ErrorCode.E_INVALID_REQUEST, protocol.ErrorCode);
        Assert.Equal("c-2", protocol.CorrelationId);
    }
}
