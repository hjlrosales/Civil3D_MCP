using System.Text.Json;
using Autodesk.Mcp.Sdk.Registration;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Dtos;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Autodesk.Mcp.Sdk.Tests;

/// <summary>Endpoint descriptor registration lifecycle (AD-03).</summary>
public class RegistrarTests
{
    [Fact]
    public async Task Register_WritesDescriptor_WithAd03WireNames()
    {
        string dir = Path.Combine(Path.GetTempPath(), "mcp-registrar-" + Guid.NewGuid().ToString("N"));
        try
        {
            var registrar = new EndpointRegistrar(
                new EndpointRegistryOptions { DirectoryPath = dir },
                NullLogger<EndpointRegistrar>.Instance);

            await registrar.RegisterAsync(new EndpointDescriptor
            {
                BridgeName = "Test.Bridge",
                Product = "Civil3D",
                ProductVersion = "2025",
                BridgeVersion = new(1, 0, 0),
                SdkVersion = new(1, 0, 0),
                ProtocolVersion = ProtocolConstants.CurrentProtocolVersion,
                PipeName = "pipe-test",
                ProcessId = 4242,
                StartedAtUtc = DateTimeOffset.Parse("2026-08-07T12:00:00Z"),
            });

            string file = Directory.GetFiles(dir).Single();
            Assert.Equal("Civil3D-4242.json", Path.GetFileName(file));
            string json = await File.ReadAllTextAsync(file);
            Assert.Contains("\"pid\":4242", json);
            Assert.Contains("\"startedUtc\":\"2026-08-07T12:00:00+00:00\"", json);

            EndpointDescriptor? read = JsonSerializer.Deserialize<EndpointDescriptor>(
                json, Autodesk.Mcp.Shared.Serialization.SharedJson.Options);
            Assert.NotNull(read);
            Assert.Equal("pipe-test", read!.PipeName);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task UpdateHeartbeat_RefreshesTimestamp()
    {
        string dir = Path.Combine(Path.GetTempPath(), "mcp-registrar-" + Guid.NewGuid().ToString("N"));
        try
        {
            var registrar = new EndpointRegistrar(
                new EndpointRegistryOptions { DirectoryPath = dir },
                NullLogger<EndpointRegistrar>.Instance);

            await registrar.RegisterAsync(new EndpointDescriptor { BridgeName = "T", Product = "Civil3D", PipeName = "p", ProcessId = 7 });
            var heartbeat = DateTimeOffset.UtcNow;
            await registrar.UpdateHeartbeatAsync(heartbeat);

            string json = await File.ReadAllTextAsync(Directory.GetFiles(dir).Single());
            EndpointDescriptor? read = JsonSerializer.Deserialize<EndpointDescriptor>(json, Autodesk.Mcp.Shared.Serialization.SharedJson.Options);
            Assert.NotNull(read!.LastHeartbeatAtUtc);
            Assert.True((read.LastHeartbeatAtUtc!.Value - heartbeat).Duration() < TimeSpan.FromSeconds(5));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Delete_RemovesDescriptor()
    {
        string dir = Path.Combine(Path.GetTempPath(), "mcp-registrar-" + Guid.NewGuid().ToString("N"));
        try
        {
            var registrar = new EndpointRegistrar(
                new EndpointRegistryOptions { DirectoryPath = dir },
                NullLogger<EndpointRegistrar>.Instance);

            await registrar.RegisterAsync(new EndpointDescriptor { BridgeName = "T", Product = "Civil3D", PipeName = "p", ProcessId = 7 });
            await registrar.DeleteAsync();

            Assert.Empty(Directory.GetFiles(dir));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
