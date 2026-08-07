using System.Text.Json;
using Autodesk.Mcp.Shared.Enums;
using Autodesk.Mcp.Shared.Errors;
using Autodesk.Mcp.Shared.Serialization;
using Xunit;

namespace Autodesk.Mcp.Shared.Tests;

/// <summary>Wire format of the manifest enums and error codes.</summary>
public class EnumSerializationTests
{
    [Fact]
    public void Enums_SerializeAsExactMemberNames()
    {
        Assert.Equal("\"ModifyDrawing\"", JsonSerializer.Serialize(ToolPermission.ModifyDrawing, SharedJson.Options));
        Assert.Equal("\"Alignments\"", JsonSerializer.Serialize(ToolCategory.Alignments, SharedJson.Options));
        Assert.Equal("\"Critical\"", JsonSerializer.Serialize(ToolRisk.Critical, SharedJson.Options));
        Assert.Equal("\"E_TIMEOUT\"", JsonSerializer.Serialize(ErrorCode.E_TIMEOUT, SharedJson.Options));
    }

    [Fact]
    public void Enums_ReadCaseInsensitively()
    {
        Assert.Equal(ToolPermission.ReadOnly, JsonSerializer.Deserialize<ToolPermission>("\"readonly\"", SharedJson.Options));
        Assert.Equal(ErrorCode.E_TIMEOUT, JsonSerializer.Deserialize<ErrorCode>("\"e_timeout\"", SharedJson.Options));
        Assert.Equal(ToolCategory.Alignments, JsonSerializer.Deserialize<ToolCategory>("\"alignments\"", SharedJson.Options));
    }

    [Fact]
    public void UnknownEnumNames_FallBackToSentinel()
    {
        Assert.Equal(ToolCategory.Unknown, JsonSerializer.Deserialize<ToolCategory>("\"QuantumLoop\"", SharedJson.Options));
        Assert.Equal(ToolPermission.Unknown, JsonSerializer.Deserialize<ToolPermission>("\"QuantumLoop\"", SharedJson.Options));
        Assert.Equal(ToolRisk.Unknown, JsonSerializer.Deserialize<ToolRisk>("\"QuantumLoop\"", SharedJson.Options));
        Assert.Equal(ErrorCode.E_UNKNOWN, JsonSerializer.Deserialize<ErrorCode>("\"E_FUTURE\"", SharedJson.Options));
    }

    [Fact]
    public void NumericEnumValues_AreAccepted()
    {
        Assert.Equal(ToolRisk.High, JsonSerializer.Deserialize<ToolRisk>("3", SharedJson.Options));
        Assert.Equal(ErrorCode.E_INTERNAL, JsonSerializer.Deserialize<ErrorCode>("11", SharedJson.Options));
    }

    [Fact]
    public void NumericStringEnumValues_AreAccepted()
    {
        Assert.Equal(ToolRisk.High, JsonSerializer.Deserialize<ToolRisk>("\"3\"", SharedJson.Options));
        Assert.Equal(ErrorCode.E_INTERNAL, JsonSerializer.Deserialize<ErrorCode>("\"11\"", SharedJson.Options));
    }
}
