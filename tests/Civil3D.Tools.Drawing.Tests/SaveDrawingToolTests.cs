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

/// <summary>save_drawing: request mapping, no-active-document, read-only and error mapping.</summary>
public class SaveDrawingToolTests
{
    [Fact]
    public async Task SuccessfulExecution_SavesWithZoomByDefault()
    {
        var save = new FakeSaveService(SampleSaveResult());
        var tool = new SaveDrawingTool(new FakeSession(SampleDrawing()), save);

        var result = Assert.IsType<SaveDrawingResult>(await ExecuteAsync(tool));

        Assert.True(result.Success);
        Assert.True(result.ZoomedToExtents);
        Assert.True(save.LastZoomToExtents, "the default request should zoom to extents");
    }

    [Fact]
    public async Task ZoomExtentsFalse_IsPassedThrough()
    {
        var save = new FakeSaveService(SampleSaveResult());
        var tool = new SaveDrawingTool(new FakeSession(SampleDrawing()), save);
        var parameters = System.Text.Json.JsonSerializer.SerializeToElement(
            new SaveDrawingRequest { ZoomExtents = false }, Autodesk.Mcp.Shared.Serialization.SharedJson.Options);

        await tool.ExecuteAsync(new ToolExecutionContext
        {
            ToolName = "save_drawing",
            CorrelationId = "c-1",
            SessionId = "s-1",
            CancellationToken = CancellationToken.None,
        }, parameters);

        Assert.False(save.LastZoomToExtents);
    }

    [Fact]
    public async Task NoActiveDocument_ThrowsNoActiveDocumentError()
    {
        var tool = new SaveDrawingTool(new FakeSession(null), new FakeSaveService(SampleSaveResult()));

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(() => ExecuteAsync(tool));

        Assert.Equal(ErrorCode.E_NO_ACTIVE_DOCUMENT, ex.ErrorCode);
    }

    [Fact]
    public async Task ReadOnlyDrawing_MapsToStableCode()
    {
        var save = new FakeSaveService((_, _) => throw new BridgeException(
            ErrorCode.E_TRANSACTION_FAILED, "read-only"));
        var tool = new SaveDrawingTool(new FakeSession(SampleDrawing()), save);

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(() => ExecuteAsync(tool));
        Assert.Equal(ErrorCode.E_TRANSACTION_FAILED, ex.ErrorCode);

        ToolCatalog catalog = CreateCatalog(save: save);
        var dispatcher = CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(Invoke("save_drawing"), CancellationToken.None);
            Assert.False(response.Success);
            Assert.Equal(ErrorCode.E_TRANSACTION_FAILED, response.ErrorCode);
        }
        finally
        {
            await dispatcher.StopAsync();
        }
    }

    [Fact]
    public async Task SaveGenericFailure_IsMappedToInternal_AndNeverExposed()
    {
        var save = new FakeSaveService((_, _) => throw new InvalidOperationException("boom"));
        var tool = new SaveDrawingTool(new FakeSession(SampleDrawing()), save);

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(() => ExecuteAsync(tool));
        Assert.Equal(ErrorCode.E_INTERNAL, ex.ErrorCode);
        Assert.DoesNotContain("boom", ex.Message);
    }
}
