using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Autodesk.Mcp.Sdk.Communication;
using Autodesk.Mcp.Sdk.Dispatch;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Autodesk.Mcp.Sdk.Tests;

/// <summary>End-to-end: real pipe host, real router, real catalog, real client.</summary>
public class PipeIntegrationTests
{
    [Fact]
    public async Task FullRoundTrip_OverRealPipe()
    {
        string pipeName = "autodesk-mcp-test-" + Guid.NewGuid().ToString("N");
        var catalog = new ToolCatalog(
            new[] { typeof(EchoTool).Assembly },
            new ManifestGenerator(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<ToolCatalog>.Instance);

        var router = new JsonRpcRouter(
            new IProtocolHandler[]
            {
                new ListToolsHandler(catalog),
                new ExecuteToolHandler(catalog, new InlineExecutor(), NullLogger<ExecuteToolHandler>.Instance),
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

            await NdjsonProtocol.WriteLineAsync(writer, "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}", CancellationToken.None);
            string? listResponse = await NdjsonProtocol.ReadLineAsync(reader, CancellationToken.None);
            Assert.NotNull(listResponse);
            Assert.Contains("test.echo", listResponse);

            await NdjsonProtocol.WriteLineAsync(writer, "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/execute\",\"params\":{\"tool\":\"test.echo\",\"arguments\":{\"text\":\"hello\"}}}", CancellationToken.None);
            string? executeResponse = await NdjsonProtocol.ReadLineAsync(reader, CancellationToken.None);
            Assert.NotNull(executeResponse);
            Assert.Contains("\"success\":true", executeResponse);

            using JsonDocument document = JsonDocument.Parse(executeResponse);
            Assert.True(document.RootElement.GetProperty("success").GetBoolean());
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private sealed class InlineExecutor : IToolExecutor
    {
        public async Task<ResponseEnvelope> ExecuteAsync(ToolInvocation invocation, CancellationToken cancellationToken)
        {
            var tool = new EchoTool();
            var context = new ToolExecutionContext
            {
                ToolName = invocation.ToolName,
                CorrelationId = invocation.CorrelationId ?? string.Empty,
                CancellationToken = cancellationToken,
            };
            object? result = await tool.ExecuteAsync(context, invocation.Parameters);
            return ResponseEnvelope.Ok(
                data: JsonSerializer.SerializeToElement(result, Autodesk.Mcp.Shared.Serialization.SharedJson.Options),
                correlationId: invocation.CorrelationId);
        }
    }
}
