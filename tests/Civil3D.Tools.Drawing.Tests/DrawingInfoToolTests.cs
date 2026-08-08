using Autodesk.Mcp.Sdk.Dispatch;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Errors;
using Civil3D.Bridge.Execution;
using Civil3D.Tools.Drawing.Dtos;
using Civil3D.Tools.Drawing.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static Civil3D.Tools.Drawing.Tests.TestDoubles;

namespace Civil3D.Tools.Drawing.Tests;

/// <summary>drawing_info: snapshot-to-DTO mapping and the no-active-document path.</summary>
public class DrawingInfoToolTests
{
    [Fact]
    public async Task SuccessfulExecution_MapsSnapshotAndVersionsToDto()
    {
        var tool = new DrawingInfoTool(new FakeSession(SampleDrawing()), new TestInfoProvider());

        var dto = Assert.IsType<DrawingInfoDto>(await ExecuteAsync(tool));

        Assert.Equal("Sample.dwg", dto.DrawingName);
        Assert.Equal(@"C:\Drawings\Sample.dwg", dto.DrawingPath);
        Assert.Equal("AC1032", dto.DrawingVersion);
        Assert.True(dto.IsModified);
        Assert.False(dto.IsReadOnly);
        Assert.Equal("Model", dto.CurrentLayout);
        Assert.True(dto.IsModelSpaceActive);
        Assert.Equal("fp-123", dto.DatabaseFingerprint);
        Assert.Equal("25.0", dto.Civil3DVersion);
        Assert.Equal("1.2.3", dto.BridgeVersion);
        Assert.Equal("4.5.6", dto.SdkVersion);
        Assert.Equal(ProtocolConstants.CurrentProtocolVersion.ToString(), dto.ProtocolVersion);
        Assert.Equal(2, dto.OpenDocumentsCount);
        Assert.Equal("Sample.dwg", dto.CurrentDocumentName);
        Assert.Equal(@"C:\Drawings\Sample.dwg", dto.CurrentDocumentPath);
    }

    [Fact]
    public async Task NoActiveDocument_ThrowsNoActiveDocumentError()
    {
        var tool = new DrawingInfoTool(new FakeSession(null), new TestInfoProvider());

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(() => ExecuteAsync(tool));

        Assert.Equal(ErrorCode.E_NO_ACTIVE_DOCUMENT, ex.ErrorCode);
        Assert.Equal("c-1", ex.CorrelationId);
        Assert.Equal("s-1", ex.SessionId);
    }

    [Fact]
    public async Task NoActiveDocument_DispatcherReturnsStableEnvelope()
    {
        ToolCatalog catalog = CreateCatalog(session: new FakeSession(null));
        var dispatcher = CreateDispatcher(catalog);

        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(Invoke("drawing_info"), CancellationToken.None);

            Assert.False(response.Success);
            Assert.Equal(ErrorCode.E_NO_ACTIVE_DOCUMENT, response.ErrorCode);
            Assert.Equal("c-1", response.CorrelationId);
        }
        finally
        {
            await dispatcher.StopAsync();
        }
    }

    [Fact]
    public async Task SuccessfulExecution_DispatcherReturnsOkWithData()
    {
        ToolCatalog catalog = CreateCatalog();
        var dispatcher = CreateDispatcher(catalog);

        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(Invoke("drawing_info"), CancellationToken.None);

            Assert.True(response.Success);
            Assert.NotNull(response.Data);
        }
        finally
        {
            await dispatcher.StopAsync();
        }
    }
}
