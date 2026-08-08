using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Shared.Dtos;
using Autodesk.Mcp.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Autodesk.Mcp.Sdk.Tests;

/// <summary>Tool discovery, manifest generation and the lazy catalog.</summary>
public class DiscoveryTests
{
    private static ToolCatalog CreateCatalog()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var generator = new ManifestGenerator();
        return new ToolCatalog(
            new[] { typeof(EchoTool).Assembly },
            generator,
            services,
            NullLogger<ToolCatalog>.Instance);
    }

    [Fact]
    public void Scan_FindsDecoratedTools()
    {
        IReadOnlyList<Type> types = ToolScanner.FindToolTypes(new[] { typeof(EchoTool).Assembly });

        Assert.Contains(types, static t => t == typeof(EchoTool));
        Assert.Contains(types, static t => t == typeof(SlowTool));
    }

    [Fact]
    public void Manifest_IsGeneratedFromType()
    {
        var generator = new ManifestGenerator();
        ToolManifest manifest = generator.Generate(typeof(EchoTool));

        Assert.Equal("test.echo", manifest.Name);
        Assert.Equal("Echo", manifest.DisplayName);
        Assert.Equal(ToolCategory.General, manifest.Category);
        Assert.Equal(ToolPermission.ReadOnly, manifest.Permission);
        Assert.Equal(new(1, 2, 3), manifest.Version);
        Assert.Contains("test", manifest.Tags);
        Assert.False(string.IsNullOrWhiteSpace(manifest.InputSchema.Root.ToJsonString()));
        Assert.False(string.IsNullOrWhiteSpace(manifest.OutputSchema.Root.ToJsonString()));
    }

    [Fact]
    public void Catalog_InstantiatesLazily_AndCaches()
    {
        ToolCatalog catalog = CreateCatalog();

        Assert.True(catalog.TryGetTool("test.echo", out var first));
        Assert.True(catalog.TryGetTool("test.echo", out var second));
        Assert.Same(first, second);
    }

    [Fact]
    public void Catalog_UnknownTool_ReturnsFalse()
    {
        ToolCatalog catalog = CreateCatalog();

        Assert.False(catalog.TryGetTool("nope", out _));
        Assert.Null(catalog.GetManifest("nope"));
        Assert.Null(catalog.GetInputSchema("nope"));
    }

    [Fact]
    public void Catalog_ExposesManifestsAndSchemas()
    {
        ToolCatalog catalog = CreateCatalog();

        Assert.Contains(catalog.Manifests, static m => m.Name == "test.echo");
        Assert.NotNull(catalog.GetInputSchema("test.echo"));
        Assert.Equal(2, catalog.ToolNames.Count);
    }

    [Fact]
    public void InputSchema_AllowsValidArgs_AndRejectsMissingRequired()
    {
        ToolCatalog catalog = CreateCatalog();
        NJsonSchema.JsonSchema schema = catalog.GetInputSchema("test.echo")!;

        Assert.Empty(schema.Validate("{\"text\":\"hi\"}"));
    }
}
