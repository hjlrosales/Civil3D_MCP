using System.Text.Json;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Errors;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Bridge.Execution;
using Civil3D.Tools.Surface.Dtos;
using Civil3D.Tools.Surface.Tools;
using Xunit;
using static Civil3D.Tools.Surface.Tests.SurfaceHarness;
using static Civil3D.Tools.Surface.Tests.TestDoubles;

namespace Civil3D.Tools.Surface.Tests;

/// <summary>
/// End-to-end through the real SDK dispatcher: tool discovery, manifest generation, request
/// routing, typed-parameter binding for <c>surface_comparison_report</c>, the workflow pipeline
/// and the protocol response envelope (success, no-active-document, missing surface, invalid
/// parameters, unknown tool).
/// </summary>
public class SurfaceComparisonReportToolTests
{
    [Fact]
    public async Task Dispatch_SurfaceComparison_ReturnsSuccessEnvelopeWithReport()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);
        ToolDispatcher dispatcher = CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(
                Invoke("surface_comparison_report", new SurfaceComparisonRequest
                {
                    ExistingSurfaceId = 1,
                    ProposedSurfaceId = 2,
                }), CancellationToken.None);

            Assert.True(response.Success);
            Assert.NotNull(response.Data);
            SurfaceComparisonReport? report =
                response.Data.Value.Deserialize<SurfaceComparisonReport>(SharedJson.Options);
            Assert.NotNull(report);
            Assert.Equal("EG", report!.Summary.ExistingSurfaceName);
            Assert.Equal("Review Required", report.Summary.Verdict);
            Assert.Equal("surface.comparison.report", report.Execution.WorkflowName);
            Assert.Equal(5, report.Execution.TotalSteps);
            Assert.NotNull(report.Statistics);
            Assert.Contains(report.Recommendations, r => r.Title == "Review before volume calculations");
        }
        finally
        {
            await dispatcher.DisposeAsync();
        }
    }

    [Fact]
    public async Task Dispatch_TogglesDisabled_OmitsStatisticsAndRecommendations()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);
        ToolDispatcher dispatcher = CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(
                Invoke("surface_comparison_report", new SurfaceComparisonRequest
                {
                    ExistingSurfaceId = 1,
                    ProposedSurfaceId = 2,
                    IncludeStatistics = false,
                    IncludeRecommendations = false,
                }), CancellationToken.None);

            Assert.True(response.Success);
            Assert.NotNull(response.Data);
            SurfaceComparisonReport? report =
                response.Data.Value.Deserialize<SurfaceComparisonReport>(SharedJson.Options);
            Assert.NotNull(report);
            Assert.Null(report!.Statistics);
            Assert.Empty(report.Recommendations);
            Assert.Equal(6, report.Metrics.Count);
            Assert.Equal(5, report.Differences.Count);
        }
        finally
        {
            await dispatcher.DisposeAsync();
        }
    }

    [Fact]
    public async Task Dispatch_MissingSurface_ReturnsObjectNotFoundEnvelope()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);
        ToolDispatcher dispatcher = CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(
                Invoke("surface_comparison_report", new SurfaceComparisonRequest
                {
                    ExistingSurfaceId = 1,
                    ProposedSurfaceId = 999,
                }), CancellationToken.None);

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
    public async Task Dispatch_IdenticalIds_ReturnsInvalidParametersEnvelope()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);
        ToolDispatcher dispatcher = CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(
                Invoke("surface_comparison_report", new SurfaceComparisonRequest
                {
                    ExistingSurfaceId = 1,
                    ProposedSurfaceId = 1,
                }), CancellationToken.None);

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
                Invoke("surface_comparison_report", new SurfaceComparisonRequest
                {
                    ExistingSurfaceId = 1,
                    ProposedSurfaceId = 2,
                }), CancellationToken.None);

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
    public void Discovery_FindsSurfaceComparisonTool()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);

        Assert.True(catalog.TryGetTool("surface_comparison_report", out ITool? tool));
        Assert.NotNull(tool);
        Assert.IsType<SurfaceComparisonReportTool>(tool);
    }

    [Fact]
    public void Manifest_GeneratesSurfaceComparisonTool()
    {
        Container container = CreateContainer();
        var generator = new ManifestGenerator();

        Autodesk.Mcp.Shared.Dtos.ToolManifest manifest = generator.Generate(typeof(SurfaceComparisonReportTool));

        Assert.Equal("surface_comparison_report", manifest.Name);
        Assert.Equal(Autodesk.Mcp.Shared.Enums.ToolCategory.Surfaces, manifest.Category);
        Assert.Equal(Autodesk.Mcp.Shared.Enums.ToolPermission.ReadOnly, manifest.Permission);
    }
}
