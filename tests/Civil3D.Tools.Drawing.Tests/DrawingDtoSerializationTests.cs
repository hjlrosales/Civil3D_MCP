using System.Text.Json;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Tools.Drawing.Dtos;
using Xunit;
using static Civil3D.Tools.Drawing.Tests.TestDoubles;

namespace Civil3D.Tools.Drawing.Tests;

/// <summary>DTO wire serialization: camelCase names and lossless round-trips.</summary>
public class DrawingDtoSerializationTests
{
    [Fact]
    public void DrawingInfoDto_SerializesWithCamelCaseNames()
    {
        var dto = new DrawingInfoDto { DrawingName = "Sample.dwg", BridgeVersion = "1.2.3", OpenDocumentsCount = 2 };

        string json = JsonSerializer.Serialize(dto, SharedJson.Options);

        Assert.Contains("\"drawingName\"", json);
        Assert.Contains("\"bridgeVersion\"", json);
        Assert.Contains("\"openDocumentsCount\"", json);
        Assert.DoesNotContain("\"DrawingName\"", json);
    }

    [Fact]
    public void DrawingInfoDto_RoundTripsLosslessly()
    {
        var dto = new DrawingInfoDto
        {
            DrawingName = "Sample.dwg",
            DrawingPath = @"C:\Drawings\Sample.dwg",
            DrawingVersion = "AC1032",
            IsModified = true,
            IsReadOnly = true,
            CurrentLayout = "Layout1",
            IsModelSpaceActive = false,
            DatabaseFingerprint = "fp-1",
            Civil3DVersion = "25.0",
            BridgeVersion = "1.0.0",
            ProtocolVersion = "1.0.0",
            SdkVersion = "1.0.0",
            OpenDocumentsCount = 1,
            CurrentDocumentName = "Sample.dwg",
            CurrentDocumentPath = @"C:\Drawings\Sample.dwg",
        };

        string json = JsonSerializer.Serialize(dto, SharedJson.Options);
        var restored = JsonSerializer.Deserialize<DrawingInfoDto>(json, SharedJson.Options);

        Assert.NotNull(restored);
        Assert.Equal(dto, restored);
    }

    [Fact]
    public void DrawingSummaryDto_SerializesWithCamelCaseNames()
    {
        var dto = new DrawingSummaryDto { LayerCount = 42, ApproximateDrawingSizeBytes = 1_000 };

        string json = JsonSerializer.Serialize(dto, SharedJson.Options);

        Assert.Contains("\"layerCount\"", json);
        Assert.Contains("\"approximateDrawingSizeBytes\"", json);
        Assert.Contains("\"modelSpaceEntityCount\"", json);
    }

    [Fact]
    public void DrawingSummaryDto_RoundTripsLosslessly()
    {
        DrawingSummaryDto dto = SampleStatisticsDto();

        string json = JsonSerializer.Serialize(dto, SharedJson.Options);
        var restored = JsonSerializer.Deserialize<DrawingSummaryDto>(json, SharedJson.Options);

        Assert.NotNull(restored);
        Assert.Equal(dto, restored);
    }

    private static DrawingSummaryDto SampleStatisticsDto() => new()
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
