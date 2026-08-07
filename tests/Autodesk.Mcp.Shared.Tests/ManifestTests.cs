using System.Text.Json;
using System.Text.Json.Nodes;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Dtos;
using Autodesk.Mcp.Shared.Enums;
using Autodesk.Mcp.Shared.Schemas;
using Autodesk.Mcp.Shared.Serialization;
using Xunit;

namespace Autodesk.Mcp.Shared.Tests;

/// <summary>Manifest and tool-manifest serialization, including embedded JSON Schemas.</summary>
public class ManifestTests
{
    private static ToolManifest SampleTool() => new()
    {
        Name = "list_alignments",
        DisplayName = "List Alignments",
        Description = "Lists all alignments in the active drawing.",
        Category = ToolCategory.Alignments,
        Version = new VersionInformation(1, 2, 0),
        Permission = ToolPermission.ReadOnly,
        Risk = ToolRisk.Low,
        TimeoutMilliseconds = 15_000,
        SupportsProgress = true,
        SupportsCancellation = true,
        InputSchema = JsonSchemaDocument.FromJson("{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"}}}"),
        OutputSchema = JsonSchemaDocument.FromJson("{\"type\":\"array\"}"),
        Examples = new[]
        {
            new ToolExample
            {
                Name = "minimal",
                Description = "One alignment",
                Input = JsonSerializer.SerializeToElement(new { name = "A1" }),
            },
        },
        Tags = new[] { "alignment", "read" },
    };

    [Fact]
    public void ToolManifest_UsesExpectedWireNames()
    {
        string json = ProtocolSerializer.Serialize(SampleTool());

        Assert.Contains("\"name\":\"list_alignments\"", json);
        Assert.Contains("\"displayName\":\"List Alignments\"", json);
        Assert.Contains("\"category\":\"Alignments\"", json);
        Assert.Contains("\"permission\":\"ReadOnly\"", json);
        Assert.Contains("\"version\":\"1.2.0\"", json);
        Assert.Contains("\"supportsProgress\":true", json);
        Assert.Contains("\"timeoutMilliseconds\":15000", json);
        Assert.Contains("\"inputSchema\":{", json);
        Assert.Contains("\"tags\":[\"alignment\",\"read\"]", json);
    }

    [Fact]
    public void Manifest_RoundTrips_IncludingSchemas()
    {
        var original = new Manifest
        {
            ProtocolVersion = ProtocolConstants.CurrentProtocolVersion,
            Tools = new[] { SampleTool() },
        };

        var result = ProtocolSerializer.Deserialize<Manifest>(ProtocolSerializer.Serialize(original))!;

        Assert.Equal(original.SchemaVersion, result.SchemaVersion);
        Assert.Equal(original.ProtocolVersion, result.ProtocolVersion);
        Assert.Single(result.Tools);
        var back = result.Tools[0];
        Assert.Equal(original.Tools[0].Name, back.Name);
        Assert.Equal(original.Tools[0].Category, back.Category);
        Assert.Equal(original.Tools[0].Permission, back.Permission);
        Assert.Equal(original.Tools[0].Version, back.Version);
        Assert.True(JsonNode.DeepEquals(original.Tools[0].InputSchema.Root, back.InputSchema.Root));
        Assert.True(JsonNode.DeepEquals(original.Tools[0].OutputSchema.Root, back.OutputSchema.Root));
        Assert.Equal(original.Tools[0].Tags, back.Tags);
        Assert.Single(back.Examples);
        Assert.Equal("minimal", back.Examples[0].Name);
        Assert.Equal(original.Tools[0].Examples[0].Input!.Value.GetRawText(), back.Examples[0].Input!.Value.GetRawText());
    }

    [Fact]
    public void DeprecatedFlag_Serializes()
    {
        string json = ProtocolSerializer.Serialize(SampleTool() with { Deprecated = true });

        Assert.Contains("\"deprecated\":true", json);
    }
}
