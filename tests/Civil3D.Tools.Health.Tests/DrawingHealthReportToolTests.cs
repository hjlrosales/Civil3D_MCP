using System.Text.Json;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Errors;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Bridge.Execution;
using Civil3D.Tools.Health.Dtos;
using Civil3D.Tools.Health.Tools;
using Xunit;
using static Civil3D.Tools.Health.Tests.HealthHarness;
using static Civil3D.Tools.Health.Tests.TestDoubles;

namespace Civil3D.Tools.Health.Tests;

/// <summary>
/// End-to-end through the real SDK dispatcher: tool discovery, manifest generation, request
/// routing, the <c>drawing_health_report</c> tool, the workflow dispatcher pipeline and the
/// protocol response envelope (success, no-active-document, unknown-tool).
/// </summary>
public class DrawingHealthReportToolTests
{
    [Fact]
    public async Task Dispatch_HealthReport_ReturnsSuccessEnvelopeWithReport()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);
        ToolDispatcher dispatcher = CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(
                Invoke("drawing_health_report"), CancellationToken.None);

            Assert.True(response.Success);
            Assert.NotNull(response.Data);
            DrawingHealthReport? report = response.Data.Value.Deserialize<DrawingHealthReport>(SharedJson.Options);
            Assert.NotNull(report);
            Assert.Equal("HealthSample.dwg", report!.DrawingName);
            Assert.Equal("drawing.health.report", report.Execution.WorkflowName);
            Assert.Equal(5, report.Execution.TotalSteps);
            Assert.Contains(report.Issues, i => i.Code == "LOCKED_COGO_POINTS");
        }
        finally
        {
            await dispatcher.DisposeAsync();
        }
    }

    [Fact]
    public async Task Dispatch_NoActiveDocument_ReturnsNoActiveDocumentEnvelope()
    {
        Container container = CreateContainer(session: new FakeSession(drawing: null));
        ToolCatalog catalog = CreateCatalog(container);
        ToolDispatcher dispatcher = CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(
                Invoke("drawing_health_report"), CancellationToken.None);

            Assert.False(response.Success);
            Assert.Equal(ErrorCode.E_NO_ACTIVE_DOCUMENT, response.ErrorCode);
            Assert.Null(response.Data);
        }
        finally
        {
            await dispatcher.DisposeAsync();
        }
    }

    [Fact]
    public async Task Dispatch_UnknownTool_ReturnsNotFoundEnvelope()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);
        ToolDispatcher dispatcher = CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(
                Invoke("no_such_tool"), CancellationToken.None);

            Assert.False(response.Success);
            Assert.Equal(ErrorCode.E_OBJECT_NOT_FOUND, response.ErrorCode);
        }
        finally
        {
            await dispatcher.DisposeAsync();
        }
    }

    [Fact]
    public void Discovery_FindsHealthReportTool()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);

        Assert.True(catalog.TryGetTool("drawing_health_report", out ITool? tool));
        Assert.NotNull(tool);
        Assert.IsType<DrawingHealthReportTool>(tool);
    }

    [Fact]
    public void Manifest_GeneratesHealthReportTool()
    {
        Container container = CreateContainer();
        var generator = new ManifestGenerator();

        Autodesk.Mcp.Shared.Dtos.ToolManifest manifest = generator.Generate(typeof(DrawingHealthReportTool));

        Assert.Equal("drawing_health_report", manifest.Name);
        Assert.Equal(Autodesk.Mcp.Shared.Enums.ToolCategory.Drawing, manifest.Category);
        Assert.Equal(Autodesk.Mcp.Shared.Enums.ToolPermission.ReadOnly, manifest.Permission);
    }
}
