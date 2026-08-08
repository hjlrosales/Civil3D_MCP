using Autodesk.Mcp.Sdk.Dispatch;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Errors;
using Civil3D.Bridge.Execution;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Drawing.Dtos;
using Civil3D.Tools.Drawing.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static Civil3D.Tools.Drawing.Tests.TestDoubles;

namespace Civil3D.Tools.Drawing.Tests;

/// <summary>drawing_summary: DTO mapping, no-active-document and error mapping.</summary>
public class DrawingSummaryToolTests
{
    [Fact]
    public async Task SuccessfulExecution_MapsStatisticsToDto()
    {
        var tool = new DrawingSummaryTool(new FakeSession(SampleDrawing()), new FakeStatisticsService(SampleStatistics()));

        var dto = Assert.IsType<DrawingSummaryDto>(await ExecuteAsync(tool));

        Assert.Equal(42, dto.LayerCount);
        Assert.Equal(13, dto.BlockCount);
        Assert.Equal(2, dto.XRefCount);
        Assert.Equal(3_000, dto.EntityCount);
        Assert.Equal(2_900, dto.ModelSpaceEntityCount);
        Assert.Equal(100, dto.PaperSpaceEntityCount);
        Assert.Equal(4, dto.ViewportCount);
        Assert.Equal(7, dto.TextStyleCount);
        Assert.Equal(3, dto.DimensionStyleCount);
        Assert.Equal(5, dto.LinetypeCount);
        Assert.Equal(6, dto.RegisteredApplicationCount);
        Assert.Equal(9, dto.DictionaryCount);
        Assert.Equal(12_345_678, dto.ApproximateDrawingSizeBytes);
    }

    [Fact]
    public async Task NoActiveDocument_ThrowsNoActiveDocumentError()
    {
        var tool = new DrawingSummaryTool(new FakeSession(null), new FakeStatisticsService(SampleStatistics()));

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(() => ExecuteAsync(tool));

        Assert.Equal(ErrorCode.E_NO_ACTIVE_DOCUMENT, ex.ErrorCode);
    }

    [Fact]
    public async Task StatisticsBridgeFailure_MapsToStableCode()
    {
        var statistics = new FakeStatisticsService(_ => throw new BridgeException(ErrorCode.E_TRANSACTION_FAILED, "scan failed"));
        var tool = new DrawingSummaryTool(new FakeSession(SampleDrawing()), statistics);

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(() => ExecuteAsync(tool));
        Assert.Equal(ErrorCode.E_TRANSACTION_FAILED, ex.ErrorCode);

        ToolCatalog catalog = CreateCatalog(statistics: statistics);
        var dispatcher = CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(Invoke("drawing_summary"), CancellationToken.None);
            Assert.False(response.Success);
            Assert.Equal(ErrorCode.E_TRANSACTION_FAILED, response.ErrorCode);
        }
        finally
        {
            await dispatcher.StopAsync();
        }
    }

    [Fact]
    public async Task StatisticsGenericFailure_IsMappedToInternal_AndNeverExposed()
    {
        var statistics = new FakeStatisticsService(_ => throw new InvalidOperationException("boom"));
        var tool = new DrawingSummaryTool(new FakeSession(SampleDrawing()), statistics);

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(() => ExecuteAsync(tool));
        Assert.Equal(ErrorCode.E_INTERNAL, ex.ErrorCode);
        Assert.DoesNotContain("boom", ex.Message);

        ToolCatalog catalog = CreateCatalog(statistics: statistics);
        var dispatcher = CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(Invoke("drawing_summary"), CancellationToken.None);
            Assert.False(response.Success);
            Assert.Equal(ErrorCode.E_INTERNAL, response.ErrorCode);
            Assert.DoesNotContain("boom", response.Message);
        }
        finally
        {
            await dispatcher.StopAsync();
        }
    }
}
