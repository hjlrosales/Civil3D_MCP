using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Tools;
using Civil3D.Bridge.Configuration;
using Civil3D.Bridge.DependencyInjection;
using Civil3D.Tools.Drawing.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using static Civil3D.Tools.Drawing.Tests.TestDoubles;

namespace Civil3D.Tools.Drawing.Tests;

/// <summary>Tool discovery, catalog registration and bridge DI wiring.</summary>
public class DrawingToolDiscoveryTests
{
    [Fact]
    public void Scanner_FindsAllDrawingTools()
    {
        IReadOnlyList<Type> types = ToolScanner.FindToolTypes(new[] { typeof(DrawingInfoTool).Assembly });

        Assert.Contains(types, static t => t == typeof(DrawingInfoTool));
        Assert.Contains(types, static t => t == typeof(DrawingSummaryTool));
        Assert.Contains(types, static t => t == typeof(SaveDrawingTool));
    }

    [Fact]
    public void Catalog_ResolvesAndCachesAllTools()
    {
        ToolCatalog catalog = CreateCatalog();

        Assert.True(catalog.TryGetTool("drawing_info", out ITool? info));
        Assert.True(catalog.TryGetTool("drawing_summary", out ITool? summary));
        Assert.True(catalog.TryGetTool("save_drawing", out ITool? save));
        Assert.True(catalog.TryGetTool("drawing_info", out ITool? infoAgain));
        Assert.Same(info, infoAgain);
        Assert.IsType<DrawingInfoTool>(info);
        Assert.IsType<DrawingSummaryTool>(summary);
        Assert.IsType<SaveDrawingTool>(save);
    }

    [Fact]
    public void Catalog_ExposesDrawingManifests()
    {
        ToolCatalog catalog = CreateCatalog();

        Assert.Contains(catalog.Manifests, static m => m.Name == "drawing_info");
        Assert.Contains(catalog.Manifests, static m => m.Name == "drawing_summary");
        Assert.Contains(catalog.Manifests, static m => m.Name == "save_drawing");
        Assert.Equal(3, catalog.ToolNames.Count);
        Assert.NotNull(catalog.GetManifest("drawing_info"));
    }

    [Fact]
    public void BridgeRegistration_ResolvesDrawingToolsAcrossLoadedAssemblies()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCivil3DBridge(new BridgeOptions
        {
            BridgeName = "Test.Bridge",
            Product = "Civil3D",
            ProductVersion = "2025",
            BridgeVersion = "1.0.0",
            SdkVersion = "1.0.0",
            SupportsCancellation = true,
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        ToolCatalog catalog = provider.GetRequiredService<ToolCatalog>();

        Assert.True(catalog.TryGetTool("drawing_info", out ITool? info));
        Assert.True(catalog.TryGetTool("drawing_summary", out ITool? summary));
        Assert.IsType<DrawingInfoTool>(info);
        Assert.IsType<DrawingSummaryTool>(summary);
        Assert.NotNull(catalog.GetManifest("drawing_info"));
        Assert.NotNull(catalog.GetManifest("drawing_summary"));
    }
}
