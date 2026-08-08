using System.Text.Json;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Errors;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Bridge.Execution;
using Civil3D.Tools.Quantity.Dtos;
using Civil3D.Tools.Quantity.Tools;
using Xunit;
using static Civil3D.Tools.Quantity.Tests.QuantityHarness;
using static Civil3D.Tools.Quantity.Tests.TestDoubles;

namespace Civil3D.Tools.Quantity.Tests;

/// <summary>
/// End-to-end through the real SDK dispatcher: tool discovery, manifest generation, request
/// routing, the <c>quantity_takeoff_report</c> tool, the workflow dispatcher pipeline and the
/// protocol response envelope (success, no-active-document, unknown-tool).
/// </summary>
public class QuantityTakeoffReportToolTests
{
    [Fact]
    public async Task Dispatch_QuantityTakeoff_ReturnsSuccessEnvelopeWithReport()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);
        ToolDispatcher dispatcher = CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(
                Invoke("quantity_takeoff_report"), CancellationToken.None);

            Assert.True(response.Success);
            Assert.NotNull(response.Data);
            QuantityTakeoffReport? report = response.Data.Value.Deserialize<QuantityTakeoffReport>(SharedJson.Options);
            Assert.NotNull(report);
            Assert.Equal("QuantitySample.dwg", report!.Overview.DrawingName);
            Assert.Equal("quantity.takeoff.report", report.Execution.WorkflowName);
            Assert.Equal(6, report.Execution.TotalSteps);
            Assert.Equal(12, report.Statistics.TotalDomainObjects);
            Assert.Contains(report.Items, i => i.Key == "alignment.total_length");
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
                Invoke("quantity_takeoff_report"), CancellationToken.None);

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
    public void Discovery_FindsQuantityTakeoffTool()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);

        Assert.True(catalog.TryGetTool("quantity_takeoff_report", out ITool? tool));
        Assert.NotNull(tool);
        Assert.IsType<QuantityTakeoffReportTool>(tool);
    }

    [Fact]
    public void Manifest_GeneratesQuantityTakeoffTool()
    {
        Container container = CreateContainer();
        var generator = new ManifestGenerator();

        Autodesk.Mcp.Shared.Dtos.ToolManifest manifest = generator.Generate(typeof(QuantityTakeoffReportTool));

        Assert.Equal("quantity_takeoff_report", manifest.Name);
        Assert.Equal(Autodesk.Mcp.Shared.Enums.ToolCategory.Drawing, manifest.Category);
        Assert.Equal(Autodesk.Mcp.Shared.Enums.ToolPermission.ReadOnly, manifest.Permission);
    }
}
