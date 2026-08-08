using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Autodesk.Mcp.Sdk.Communication;
using Autodesk.Mcp.Sdk.Dispatch;
using Autodesk.Mcp.Sdk.Discovery;
using Civil3D.Bridge.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static Civil3D.Tools.Drawing.Tests.TestDoubles;

namespace Civil3D.Tools.Drawing.Tests;

/// <summary>
/// End-to-end execution path: tool discovery -> manifest generation -> request routing -> dispatcher
/// -> tool execution -> protocol response, over a real named pipe with mocked Autodesk services.
/// </summary>
public class DrawingToolIntegrationTests
{
    [Fact]
    public async Task FullRoundTrip_OverRealPipe()
    {
        string pipeName = "autodesk-mcp-civil3d-test-" + Guid.NewGuid().ToString("N");
        ToolCatalog catalog = CreateCatalog();
        var dispatcher = new ToolDispatcher(catalog, new InlineContext(), new CancellationRegistry(), NullLogger<ToolDispatcher>.Instance);
        dispatcher.Start();

        try
        {
            var router = new JsonRpcRouter(
                new IProtocolHandler[]
                {
                    new ListToolsHandler(catalog),
                    new ExecuteToolHandler(catalog, dispatcher, NullLogger<ExecuteToolHandler>.Instance),
                },
                new CancellationRegistry(),
                NullLogger<JsonRpcRouter>.Instance);

            var host = new NamedPipeServerHost(pipeName, 4, router, NullLogger<NamedPipeServerHost>.Instance);
            await host.StartAsync();

            try
            {
                using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await client.ConnectAsync(10_000);
                using var writer = new StreamWriter(client, new UTF8Encoding(false), bufferSize: 4096, leaveOpen: true) { AutoFlush = true };
                using var reader = new StreamReader(client, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);

                // 1) Discovery + manifest generation: tools/list serves both drawing tools.
                await NdjsonProtocol.WriteLineAsync(writer, "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}", CancellationToken.None);
                string? listResponse = await NdjsonProtocol.ReadLineAsync(reader, CancellationToken.None);
                Assert.NotNull(listResponse);
                using (JsonDocument listDoc = JsonDocument.Parse(listResponse))
                {
                    Assert.True(listDoc.RootElement.GetProperty("success").GetBoolean());
                    JsonElement tools = listDoc.RootElement.GetProperty("data").GetProperty("tools");
                    Assert.Contains(tools.EnumerateArray(), static t => t.GetProperty("name").GetString() == "drawing_info");
                    Assert.Contains(tools.EnumerateArray(), static t => t.GetProperty("name").GetString() == "drawing_summary");
                }

                // 2) Request routing -> dispatcher -> tool execution -> protocol response.
                await NdjsonProtocol.WriteLineAsync(writer, "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/execute\",\"params\":{\"tool\":\"drawing_info\"}}", CancellationToken.None);
                string? infoResponse = await NdjsonProtocol.ReadLineAsync(reader, CancellationToken.None);
                Assert.NotNull(infoResponse);
                using (JsonDocument infoDoc = JsonDocument.Parse(infoResponse))
                {
                    Assert.True(infoDoc.RootElement.GetProperty("success").GetBoolean());
                    Assert.Equal("Sample.dwg", infoDoc.RootElement.GetProperty("data").GetProperty("drawingName").GetString());
                    Assert.Equal("1.2.3", infoDoc.RootElement.GetProperty("data").GetProperty("bridgeVersion").GetString());
                }

                await NdjsonProtocol.WriteLineAsync(writer, "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/execute\",\"params\":{\"tool\":\"drawing_summary\"}}", CancellationToken.None);
                string? summaryResponse = await NdjsonProtocol.ReadLineAsync(reader, CancellationToken.None);
                Assert.NotNull(summaryResponse);
                using (JsonDocument summaryDoc = JsonDocument.Parse(summaryResponse))
                {
                    Assert.True(summaryDoc.RootElement.GetProperty("success").GetBoolean());
                    Assert.Equal(42, summaryDoc.RootElement.GetProperty("data").GetProperty("layerCount").GetInt32());
                    Assert.Equal(4, summaryDoc.RootElement.GetProperty("data").GetProperty("viewportCount").GetInt32());
                }

                // 3) No active document maps to the stable E_NO_ACTIVE_DOCUMENT envelope.
                ToolCatalog emptyCatalog = CreateCatalog(session: new FakeSession(null));
                var emptyDispatcher = new ToolDispatcher(emptyCatalog, new InlineContext(), new CancellationRegistry(), NullLogger<ToolDispatcher>.Instance);
                emptyDispatcher.Start();
                try
                {
                    var emptyRouter = new JsonRpcRouter(
                        new IProtocolHandler[] { new ExecuteToolHandler(emptyCatalog, emptyDispatcher, NullLogger<ExecuteToolHandler>.Instance) },
                        new CancellationRegistry(),
                        NullLogger<JsonRpcRouter>.Instance);
                    string emptyPipeName = "autodesk-mcp-civil3d-empty-" + Guid.NewGuid().ToString("N");
                    var emptyHost = new NamedPipeServerHost(emptyPipeName, 1, emptyRouter, NullLogger<NamedPipeServerHost>.Instance);
                    await emptyHost.StartAsync();
                    try
                    {
                        using var emptyClient = new NamedPipeClientStream(".", emptyPipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                        await emptyClient.ConnectAsync(10_000);
                        using var emptyWriter = new StreamWriter(emptyClient, new UTF8Encoding(false), bufferSize: 4096, leaveOpen: true) { AutoFlush = true };
                        using var emptyReader = new StreamReader(emptyClient, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
                        await NdjsonProtocol.WriteLineAsync(emptyWriter, "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/execute\",\"params\":{\"tool\":\"drawing_info\"}}", CancellationToken.None);
                        string? errorResponse = await NdjsonProtocol.ReadLineAsync(emptyReader, CancellationToken.None);
                        Assert.NotNull(errorResponse);
                        using JsonDocument errorDoc = JsonDocument.Parse(errorResponse);
                        Assert.False(errorDoc.RootElement.GetProperty("success").GetBoolean());
                        Assert.Equal("E_NO_ACTIVE_DOCUMENT", errorDoc.RootElement.GetProperty("errorCode").GetString());
                    }
                    finally
                    {
                        await emptyHost.StopAsync();
                    }
                }
                finally
                {
                    await emptyDispatcher.StopAsync();
                }
            }
            finally
            {
                await host.StopAsync();
            }
        }
        finally
        {
            await dispatcher.StopAsync();
        }
    }
}
