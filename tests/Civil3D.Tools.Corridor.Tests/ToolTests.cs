using System.Text.Json;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Errors;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Bridge.Execution;
using Civil3D.Tools.Corridor.Dtos;
using Civil3D.Tools.Corridor.Tools;
using Xunit;
using static Civil3D.Tools.Corridor.Tests.CorridorHarness;
using static Civil3D.Tools.Corridor.Tests.TestDoubles;

namespace Civil3D.Tools.Corridor.Tests;

/// <summary>
/// End-to-end through the real SDK dispatcher: tool discovery, manifest generation, request
/// routing, typed-parameter binding for <c>corridor_analysis_report</c> (optional corridor id),
/// the workflow pipeline and the protocol response envelope (success, no-active-document,
/// missing corridor, invalid parameters, unknown tool).
/// </summary>
public class CorridorAnalysisReportToolTests
{
    [Fact]
    public async Task Dispatch_AllCorridors_ReturnsSuccessEnvelopeWithReport()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);
        ToolDispatcher dispatcher = CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(
                Invoke("corridor_analysis_report", new CorridorAnalysisRequest()),
                CancellationToken.None);

            Assert.True(response.Success);
            Assert.NotNull(response.Data);
            CorridorAnalysisReport? report =
                response.Data.Value.Deserialize<CorridorAnalysisReport>(SharedJson.Options);
            Assert.NotNull(report);
            Assert.Equal("Attention Required", report!.Verdict);
            Assert.Equal(3, report.Corridors.Count);
            Assert.Equal(4, report.Recommendations.Count);
            Assert.Equal("corridor.analysis.report", report.Execution.WorkflowName);
            Assert.Equal(5, report.Execution.TotalSteps);
        }
        finally
        {
            await dispatcher.DisposeAsync();
        }
    }

    [Fact]
    public async Task Dispatch_SingleCorridor_TypedBindingAnalyzesOnlyThatCorridor()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);
        ToolDispatcher dispatcher = CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(
                Invoke("corridor_analysis_report", new CorridorAnalysisRequest { CorridorId = 2 }),
                CancellationToken.None);

            Assert.True(response.Success);
            Assert.NotNull(response.Data);
            CorridorAnalysisReport? report =
                response.Data.Value.Deserialize<CorridorAnalysisReport>(SharedJson.Options);
            Assert.NotNull(report);
            CorridorSummary summary = Assert.Single(report!.Corridors);
            Assert.Equal("Ramp A", summary.Name);
            Assert.Equal("No Surfaces", summary.Status);
        }
        finally
        {
            await dispatcher.DisposeAsync();
        }
    }

    [Fact]
    public async Task Dispatch_MissingCorridor_ReturnsObjectNotFoundEnvelope()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);
        ToolDispatcher dispatcher = CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(
                Invoke("corridor_analysis_report", new CorridorAnalysisRequest { CorridorId = 999 }),
                CancellationToken.None);

            Assert.False(response.Success);
            Assert.Equal(ErrorCode.E_OBJECT_NOT_FOUND, response.ErrorCode);
            Assert.Null(response.Data);
        }
        finally
        {
            await dispatcher.DisposeAsync();
        }
    }

    [Fact]
    public async Task Dispatch_InvalidId_ReturnsInvalidParametersEnvelope()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);
        ToolDispatcher dispatcher = CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(
                Invoke("corridor_analysis_report", new CorridorAnalysisRequest { CorridorId = 0 }),
                CancellationToken.None);

            Assert.False(response.Success);
            Assert.Equal(ErrorCode.E_INVALID_PARAMETERS, response.ErrorCode);
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
                Invoke("corridor_analysis_report", new CorridorAnalysisRequest()),
                CancellationToken.None);

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
    public void Discovery_FindsCorridorAnalysisReportTool()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);

        Assert.True(catalog.TryGetTool("corridor_analysis_report", out ITool? tool));
        Assert.NotNull(tool);
        Assert.IsType<CorridorAnalysisReportTool>(tool);
    }

    [Fact]
    public void Manifest_GeneratesCorridorAnalysisReportTool()
    {
        Container container = CreateContainer();
        var generator = new ManifestGenerator();

        Autodesk.Mcp.Shared.Dtos.ToolManifest manifest = generator.Generate(typeof(CorridorAnalysisReportTool));

        Assert.Equal("corridor_analysis_report", manifest.Name);
        Assert.Equal(Autodesk.Mcp.Shared.Enums.ToolCategory.Corridors, manifest.Category);
        Assert.Equal(Autodesk.Mcp.Shared.Enums.ToolPermission.ReadOnly, manifest.Permission);
    }
}
