using System.Text.Json;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Errors;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Bridge.Execution;
using Civil3D.Tools.Validation.Dtos;
using Civil3D.Tools.Validation.Tools;
using Xunit;
using static Civil3D.Tools.Validation.Tests.ValidationHarness;
using static Civil3D.Tools.Validation.Tests.TestDoubles;

namespace Civil3D.Tools.Validation.Tests;

/// <summary>
/// End-to-end through the real SDK dispatcher: tool discovery, manifest generation, request
/// routing, the <c>design_validation_report</c> tool, the workflow dispatcher pipeline and the
/// protocol response envelope (success, no-active-document, unknown-tool).
/// </summary>
public class DesignValidationReportToolTests
{
    [Fact]
    public async Task Dispatch_ValidationReport_ReturnsSuccessEnvelopeWithReport()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);
        ToolDispatcher dispatcher = CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(
                Invoke("design_validation_report"), CancellationToken.None);

            Assert.True(response.Success);
            Assert.NotNull(response.Data);
            DesignValidationReport? report = response.Data.Value.Deserialize<DesignValidationReport>(SharedJson.Options);
            Assert.NotNull(report);
            Assert.Equal("ValidationSample.dwg", report!.DrawingName);
            Assert.Equal("design.validation.report", report.Execution.WorkflowName);
            Assert.Equal(5, report.Execution.TotalSteps);
            Assert.Equal(8, report.Summary.RulesExecuted);
            Assert.Contains(report.Issues, i => i.Code == "DUPLICATE_COGO_POINT_NUMBER");
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
                Invoke("design_validation_report"), CancellationToken.None);

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
    public void Discovery_FindsValidationReportTool()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);

        Assert.True(catalog.TryGetTool("design_validation_report", out ITool? tool));
        Assert.NotNull(tool);
        Assert.IsType<DesignValidationReportTool>(tool);
    }

    [Fact]
    public void Manifest_GeneratesValidationReportTool()
    {
        Container container = CreateContainer();
        var generator = new ManifestGenerator();

        Autodesk.Mcp.Shared.Dtos.ToolManifest manifest = generator.Generate(typeof(DesignValidationReportTool));

        Assert.Equal("design_validation_report", manifest.Name);
        Assert.Equal(Autodesk.Mcp.Shared.Enums.ToolCategory.Drawing, manifest.Category);
        Assert.Equal(Autodesk.Mcp.Shared.Enums.ToolPermission.ReadOnly, manifest.Permission);
    }
}
