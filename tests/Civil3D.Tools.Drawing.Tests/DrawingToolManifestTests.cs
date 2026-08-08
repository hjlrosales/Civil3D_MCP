using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Dtos;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Tools.Drawing.Tools;
using Xunit;

namespace Civil3D.Tools.Drawing.Tests;

/// <summary>Manifest generation for the drawing tools (discovery contract).</summary>
public class DrawingToolManifestTests
{
    private static readonly ManifestGenerator Generator = new();

    [Fact]
    public void DrawingInfo_GeneratesManifest()
    {
        ToolManifest manifest = Generator.Generate(typeof(DrawingInfoTool));

        Assert.Equal("drawing_info", manifest.Name);
        Assert.Equal("Drawing Info", manifest.DisplayName);
        Assert.Equal(ToolCategory.Drawing, manifest.Category);
        Assert.Equal(ToolPermission.ReadOnly, manifest.Permission);
        Assert.Equal(ToolRisk.Low, manifest.Risk);
        Assert.Equal(new VersionInformation(1, 0, 0), manifest.Version);
        Assert.True(manifest.SupportsCancellation);
        Assert.Equal(ProtocolConstants.DefaultToolTimeoutMilliseconds, manifest.TimeoutMilliseconds);
        Assert.Contains("drawing", manifest.Tags);
        Assert.False(string.IsNullOrWhiteSpace(manifest.InputSchema.Root.ToJsonString()));
        Assert.False(string.IsNullOrWhiteSpace(manifest.OutputSchema.Root.ToJsonString()));
        Assert.Contains("drawingName", manifest.OutputSchema.Root.ToJsonString());
    }

    [Fact]
    public void DrawingSummary_GeneratesManifest()
    {
        ToolManifest manifest = Generator.Generate(typeof(DrawingSummaryTool));

        Assert.Equal("drawing_summary", manifest.Name);
        Assert.Equal("Drawing Summary", manifest.DisplayName);
        Assert.Equal(ToolCategory.Drawing, manifest.Category);
        Assert.Equal(ToolPermission.ReadOnly, manifest.Permission);
        Assert.Equal(ToolRisk.Low, manifest.Risk);
        Assert.Equal(new VersionInformation(1, 0, 0), manifest.Version);
        Assert.True(manifest.SupportsCancellation);
        Assert.Contains("layerCount", manifest.OutputSchema.Root.ToJsonString());
        Assert.Contains("approximateDrawingSizeBytes", manifest.OutputSchema.Root.ToJsonString());
    }
}
