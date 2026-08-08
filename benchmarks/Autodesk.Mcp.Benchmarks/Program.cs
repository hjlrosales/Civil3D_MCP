using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Autodesk.Mcp.Sdk.Communication;
using Autodesk.Mcp.Sdk.Dispatch;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Hosting;
using Autodesk.Mcp.Sdk.Registration;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Dtos;
using Autodesk.Mcp.Shared.Enums;
using Autodesk.Mcp.Shared.Schemas;
using Autodesk.Mcp.Shared.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NJsonSchema;

namespace Autodesk.Mcp.Benchmarks;

/// <summary>
/// Self-contained performance harness (Stopwatch-based, no external packages).
/// Covers: protocol serialization, handshake, tool discovery (manifest generation),
/// large manifest loading, named-pipe round-trip throughput, reconnect latency,
/// bridge startup and memory usage for manifest handling.
/// </summary>
internal static class Program
{
    private const string ResultsDir = "results";

    private static readonly List<(string Name, string Value)> Results = new();

    internal static async Task<int> Main()
    {
        Console.WriteLine("# Autodesk MCP Platform benchmarks");
        Console.WriteLine($"# {DateTimeOffset.UtcNow:O}");
        Console.WriteLine($"# .NET {Environment.Version} on {RuntimeInformationString}");
        Console.WriteLine();

        // Warmup JIT / first-run costs.
        Warmup();

        await MeasureEnvelopeRoundTripAsync(iterations: 20_000);
        MeasureHandshakeRoundTrip(iterations: 20_000);
        MeasureManifestGeneration(iterations: 20);
        MeasureLargeManifest(iterations: 200);
        await MeasurePipeRoundTripAsync(iterations: 1_000);
        await MeasureReconnectAsync(connections: 20);
        await MeasureStartupAsync(starts: 10);
        MeasureManifestMemory();

        PrintTable();

        Directory.CreateDirectory(ResultsDir);
        string file = Path.Combine(ResultsDir, "benchmarks.md");
        WriteResultsFile(file);
        Console.WriteLine($"\nResults written to {file}.");
        return 0;
    }

    private static string RuntimeInformationString =>
        System.Runtime.InteropServices.RuntimeInformation.OSDescription + " / " +
        System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;

    private static void Warmup()
    {
        _ = JsonSerializer.Serialize(RequestEnvelope.Create(ProtocolConstants.HealthPing), SharedJson.Options);
        _ = new ManifestGenerator().Generate(typeof(EchoTool));
        Console.WriteLine("Warmup complete.\n");
    }

    // ---------------- 1. Request/Response envelope round-trip ----------------

    private static async Task MeasureEnvelopeRoundTripAsync(int iterations)
    {
        var request = RequestEnvelope.Create(
            ProtocolConstants.ToolsExecute,
            parameters: JsonSerializer.SerializeToElement(new { tool = "bench.echo", arguments = new { text = "hello" } }),
            correlationId: Guid.NewGuid().ToString("N"));
        var response = ResponseEnvelope.Ok(
            data: JsonSerializer.SerializeToElement(new { text = "hello" }, SharedJson.Options),
            correlationId: request.CorrelationId);

        await MeasureAsync("envelope round-trip (request+response)", iterations, () =>
        {
            string json = JsonSerializer.Serialize(request, SharedJson.Options) + "|" + JsonSerializer.Serialize(response, SharedJson.Options);
            _ = JsonSerializer.Deserialize<RequestEnvelope>(json.Split('|')[0], SharedJson.Options);
            _ = JsonSerializer.Deserialize<ResponseEnvelope>(json.Split('|')[1], SharedJson.Options);
            return Task.CompletedTask;
        });
    }

    // ---------------- 2. Handshake DTO round-trip ----------------

    private static void MeasureHandshakeRoundTrip(int iterations)
    {
        var request = new HandshakeRequest
        {
            ProtocolVersion = ProtocolConstants.CurrentProtocolVersion,
            ClientName = "Autodesk.MCP.Server",
            ClientVersion = "1.0.0-rc.1",
            Capabilities = new ClientCapabilities { SupportsConfirmation = true, SupportsProgress = true, SupportsCancellation = true },
        };
        var response = new HandshakeResponse
        {
            ProtocolVersion = ProtocolConstants.CurrentProtocolVersion,
            SessionId = "sess-1",
            Bridge = new BridgeInformation
            {
                BridgeName = "Civil3D.Bridge",
                Product = "Civil3D",
                ProductVersion = "2025",
                BridgeVersion = new VersionInformation(1, 0, 0),
                SdkVersion = new VersionInformation(1, 0, 0),
                ProtocolVersion = ProtocolConstants.CurrentProtocolVersion,
            },
        };

        Measure("handshake DTO round-trip", iterations, () =>
        {
            string reqJson = JsonSerializer.Serialize(request, SharedJson.Options);
            string resJson = JsonSerializer.Serialize(response, SharedJson.Options);
            _ = JsonSerializer.Deserialize<HandshakeRequest>(reqJson, SharedJson.Options);
            _ = JsonSerializer.Deserialize<HandshakeResponse>(resJson, SharedJson.Options);
        });
    }

    // ---------------- 3. Tool discovery (manifest generation) ----------------

    private static void MeasureManifestGeneration(int iterations)
    {
        var generator = new ManifestGenerator();
        Type[] tools = { typeof(EchoTool), typeof(ListAlignmentsTool), typeof(CalculateCutFillTool) };
        Measure("manifest generation (3 tools, NJsonSchema)", iterations, () =>
        {
            foreach (Type tool in tools)
            {
                _ = generator.Generate(tool);
            }
        });
    }

    // ---------------- 4. Large manifest loading ----------------

    private static void MeasureLargeManifest(int iterations)
    {
        Manifest manifest = BuildLargeManifest(toolCount: 200);
        string serialized = JsonSerializer.Serialize(manifest, SharedJson.Options);
        long bytes = Encoding.UTF8.GetByteCount(serialized);
        Results.Add(("large manifest wire size (200 tools)", $"{bytes / 1024.0:F1} KB"));

        Measure("large manifest serialize+deserialize (200 tools)", iterations, () =>
        {
            string json = JsonSerializer.Serialize(manifest, SharedJson.Options);
            _ = JsonSerializer.Deserialize<Manifest>(json, SharedJson.Options);
        });
    }

    private static Manifest BuildLargeManifest(int toolCount)
    {
        var tools = new List<ToolManifest>(toolCount);
        for (int i = 0; i < toolCount; i++)
        {
            tools.Add(new ToolManifest
            {
                Name = $"bench.tool_{i}",
                DisplayName = $"Tool {i}",
                Description = "Generated benchmark tool with a representative schema.",
                Category = ToolCategory.Engineering,
                Permission = ToolPermission.ReadOnly,
                Risk = ToolRisk.Low,
                Version = new VersionInformation(1, 0, 0),
                TimeoutMilliseconds = ProtocolConstants.DefaultToolTimeoutMilliseconds,
                SupportsProgress = false,
                SupportsCancellation = false,
                InputSchema = JsonSchemaDocument.FromJson("{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"}},\"additionalProperties\":false}"),
                OutputSchema = JsonSchemaDocument.FromJson("{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}"),
            });
        }

        return new Manifest
        {
            SchemaVersion = 1,
            ProtocolVersion = ProtocolConstants.CurrentProtocolVersion,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Tools = tools,
        };
    }

    // ---------------- 5. Named-pipe round-trip throughput ----------------

    private static async Task MeasurePipeRoundTripAsync(int iterations)
    {
        string pipeName = "autodesk-mcp-bench-" + Guid.NewGuid().ToString("N");
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

            string requestJson = JsonSerializer.Serialize(
                RequestEnvelope.Create(
                    ProtocolConstants.ToolsExecute,
                    parameters: JsonSerializer.SerializeToElement(new { tool = "bench.echo", arguments = new { text = "hello" } }),
                    correlationId: Guid.NewGuid().ToString("N")),
                SharedJson.Options);

            // Warmup one round trip.
            await NdjsonProtocol.WriteLineAsync(writer, requestJson, CancellationToken.None);
            _ = await NdjsonProtocol.ReadLineAsync(reader, CancellationToken.None);

            long payloadBytes = Encoding.UTF8.GetByteCount(requestJson);
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                await NdjsonProtocol.WriteLineAsync(writer, requestJson, CancellationToken.None);
                _ = await NdjsonProtocol.ReadLineAsync(reader, CancellationToken.None);
            }

            stopwatch.Stop();
            double avgUs = stopwatch.Elapsed.TotalMilliseconds * 1000 / iterations;
            double opsPerSec = iterations / stopwatch.Elapsed.TotalSeconds;
            double mbPerSec = (payloadBytes * 2 * iterations) / (1024.0 * 1024.0) / stopwatch.Elapsed.TotalSeconds;
            Results.Add(("pipe round-trip (avg)", $"{avgUs:F0} us"));
            Results.Add(("pipe round-trip (throughput)", $"{opsPerSec:F0} ops/s"));
            Results.Add(("pipe throughput (request+response)", $"{mbPerSec:F1} MB/s"));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    // ---------------- 6. Reconnect latency ----------------

    private static async Task MeasureReconnectAsync(int connections)
    {
        string pipeName = "autodesk-mcp-bench-reconnect-" + Guid.NewGuid().ToString("N");
        var router = new JsonRpcRouter(
            new IProtocolHandler[] { new ListToolsHandler(new FakeCatalog()) },
            new CancellationRegistry(),
            NullLogger<JsonRpcRouter>.Instance);
        var host = new NamedPipeServerHost(pipeName, 4, router, NullLogger<NamedPipeServerHost>.Instance);
        await host.StartAsync();
        try
        {
            var times = new List<double>(connections);
            for (int i = 0; i < connections; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await client.ConnectAsync(10_000);
                stopwatch.Stop();
                times.Add(stopwatch.Elapsed.TotalMilliseconds);
            }

            Results.Add(("reconnect latency (connect, avg)", $"{times.Average():F1} ms"));
            Results.Add(("reconnect latency (connect, p95)", $"{Percentile(times, 95):F1} ms"));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    // ---------------- 7. Bridge startup ----------------

    private static async Task MeasureStartupAsync(int starts)
    {
        var times = new List<double>(starts);
        for (int i = 0; i < starts; i++)
        {
            string pipeName = "autodesk-mcp-bench-start-" + Guid.NewGuid().ToString("N");
            var info = new StaticInfoProvider(pipeName);
            var registrar = new InMemoryRegistrar();
            var options = new BridgeHostOptions
            {
                BridgeName = "Civil3D.Bridge",
                Product = "Civil3D",
                ProductVersion = "2025",
                BridgeVersion = new VersionInformation(1, 0, 0),
                SdkVersion = new VersionInformation(1, 0, 0),
                PipeName = pipeName,
                MaxConcurrentConnections = 8,
            };
            var router = new JsonRpcRouter(
                new IProtocolHandler[] { new ListToolsHandler(new FakeCatalog()) },
                new CancellationRegistry(),
                NullLogger<JsonRpcRouter>.Instance);
            var pipeHost = new NamedPipeServerHost(pipeName, 4, router, NullLogger<NamedPipeServerHost>.Instance);
            var bridgeHost = new BridgeHost(pipeHost, registrar, info, options, new BridgeShutdown(), NullLogger<BridgeHost>.Instance);

            var stopwatch = Stopwatch.StartNew();
            await bridgeHost.StartAsync();
            stopwatch.Stop();
            times.Add(stopwatch.Elapsed.TotalMilliseconds);
            await bridgeHost.StopAsync();
        }

        Results.Add(("bridge startup (host start)", $"{times.Average():F1} ms avg, p95 {Percentile(times, 95):F1} ms"));
    }

    // ---------------- 8. Memory usage ----------------

    private static void MeasureManifestMemory()
    {
        Manifest manifest = BuildLargeManifest(toolCount: 200);
        var generator = new ManifestGenerator();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        long before = GC.GetTotalMemory(true);

        for (int i = 0; i < 10; i++)
        {
            _ = JsonSerializer.Serialize(manifest, SharedJson.Options);
            _ = generator.Generate(typeof(ListAlignmentsTool));
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        long after = GC.GetTotalMemory(true);
        Results.Add(("managed heap delta (manifest work)", $"{(after - before) / 1024.0 / 1024.0:F1} MB"));
    }

    // ---------------- measurement helpers ----------------

    private static void Measure(string name, int iterations, Action action)
    {
        action(); // warmup
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            action();
        }

        stopwatch.Stop();
        double avgUs = stopwatch.Elapsed.TotalMilliseconds * 1000 / iterations;
        Results.Add((name, $"{avgUs:F0} us/op ({iterations:N0} iters, total {stopwatch.Elapsed.TotalMilliseconds:F0} ms)"));
    }

    private static async Task MeasureAsync(string name, int iterations, Func<Task> action)
    {
        await action(); // warmup
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            await action();
        }

        stopwatch.Stop();
        double avgUs = stopwatch.Elapsed.TotalMilliseconds * 1000 / iterations;
        Results.Add((name, $"{avgUs:F0} us/op ({iterations:N0} iters, total {stopwatch.Elapsed.TotalMilliseconds:F0} ms)"));
    }

    private static double Percentile(List<double> values, int percentile)
    {
        List<double> sorted = values.OrderBy(static v => v).ToList();
        int index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }

    private static void PrintTable()
    {
        Console.WriteLine("| Metric | Result |");
        Console.WriteLine("| --- | --- |");
        foreach ((string name, string value) in Results)
        {
            Console.WriteLine($"| {name} | {value} |");
        }
    }

    private static void WriteResultsFile(string path)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Autodesk MCP Platform benchmarks");
        builder.AppendLine($"- Date: {DateTimeOffset.UtcNow:O}");
        builder.AppendLine($"- .NET: {Environment.Version} ({RuntimeInformationString})");
        builder.AppendLine();
        builder.AppendLine("| Metric | Result |");
        builder.AppendLine("| --- | --- |");
        foreach ((string name, string value) in Results)
        {
            builder.AppendLine($"| {name} | {value} |");
        }

        File.WriteAllText(path, builder.ToString());
    }

    // ---------------- fakes ----------------

    private sealed class FakeCatalog : IToolCatalog
    {
        private static readonly ToolManifest Echo = new ManifestGenerator().Generate(typeof(EchoTool));

        public IReadOnlyList<ToolManifest> Manifests => new[] { Echo };

        public IReadOnlyCollection<string> ToolNames => new[] { Echo.Name };

        public bool TryGetTool(string name, out ITool tool)
        {
            tool = new EchoTool();
            return name == Echo.Name;
        }

        public ToolManifest? GetManifest(string name) => name == Echo.Name ? Echo : null;

        public JsonSchema? GetInputSchema(string name) => null;
    }

    private sealed class InMemoryRegistrar : IEndpointRegistrar
    {
        public bool Registered { get; private set; }

        public Task RegisterAsync(EndpointDescriptor descriptor, CancellationToken cancellationToken = default)
        {
            Registered = true;
            return Task.CompletedTask;
        }

        public Task UpdateHeartbeatAsync(DateTimeOffset timestamp, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAsync()
        {
            Registered = false;
            return Task.CompletedTask;
        }
    }

    private sealed class StaticInfoProvider : IEndpointInfoProvider
    {
        private readonly string _pipeName;

        public StaticInfoProvider(string pipeName) => _pipeName = pipeName;

        public BridgeInformation GetBridgeInformation() => new()
        {
            BridgeName = "Civil3D.Bridge",
            Product = "Civil3D",
            ProductVersion = "2025",
            BridgeVersion = new VersionInformation(1, 0, 0),
            SdkVersion = new VersionInformation(1, 0, 0),
            ProtocolVersion = ProtocolConstants.CurrentProtocolVersion,
        };

        public EndpointDescriptor CreateEndpointDescriptor() => new()
        {
            BridgeName = "Civil3D.Bridge",
            Product = "Civil3D",
            ProductVersion = "2025",
            BridgeVersion = new VersionInformation(1, 0, 0),
            SdkVersion = new VersionInformation(1, 0, 0),
            ProtocolVersion = new VersionInformation(1, 0, 0),
            PipeName = _pipeName,
            ProcessId = Environment.ProcessId,
            StartedAtUtc = DateTimeOffset.UtcNow,
        };
    }
}
