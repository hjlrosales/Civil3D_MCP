using System.Text.Json;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Errors;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Bridge.Execution;
using Civil3D.Tools.CutFill.Abstractions;
using Civil3D.Tools.CutFill.Dtos;
using Civil3D.Tools.CutFill.Tools;
using Xunit;
using static Civil3D.Tools.CutFill.Tests.CutFillHarness;
using static Civil3D.Tools.CutFill.Tests.TestDoubles;

namespace Civil3D.Tools.CutFill.Tests;

/// <summary>
/// End-to-end through the real SDK dispatcher: tool discovery, manifest generation, request
/// routing, typed-parameter binding for <c>calculate_cut_fill</c>, the workflow pipeline and
/// the protocol response envelope (success, no-active-document, missing surface, invalid
/// parameters, not-supported calculator, unknown tool).
/// </summary>
public class CalculateCutFillToolTests
{
    [Fact]
    public async Task Dispatch_CalculateCutFill_ReturnsSuccessEnvelopeWithReport()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);
        ToolDispatcher dispatcher = CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(
                Invoke("calculate_cut_fill", new CutFillRequest
                {
                    ExistingSurfaceId = 1,
                    ProposedSurfaceId = 2,
                }), CancellationToken.None);

            Assert.True(response.Success);
            Assert.NotNull(response.Data);
            CutFillReport? report = response.Data?.Deserialize<CutFillReport>(SharedJson.Options);
            Assert.NotNull(report);
            Assert.Equal("EG", report!.Summary.ExistingSurfaceName);
            Assert.Equal("Predominantly Cut", report.Summary.Verdict);
            Assert.Equal(12_000, report.Summary.CutVolume);
            Assert.Equal("calculate.cut.fill", report.Execution.WorkflowName);
            Assert.Equal(6, report.Execution.TotalSteps);
            Assert.NotNull(report.Statistics);
            Assert.Contains(report.Recommendations, r => r.Title == "Significant net export");
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
                Invoke("calculate_cut_fill", new CutFillRequest
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
                Invoke("calculate_cut_fill", new CutFillRequest
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
                Invoke("calculate_cut_fill", new CutFillRequest
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
    public async Task Dispatch_NotSupportedCalculator_ReturnsReportWithStructuredVerdict()
    {
        Container container = CreateContainer(calculator: new FakeCutFillCalculator(
            new CutFillCalculationResult
            {
                Status = CutFillStatus.NotSupported,
                NotSupportedReason = "Read-only volumes are unavailable.",
            }));
        ToolCatalog catalog = CreateCatalog(container);
        ToolDispatcher dispatcher = CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(
                Invoke("calculate_cut_fill", new CutFillRequest
                {
                    ExistingSurfaceId = 1,
                    ProposedSurfaceId = 2,
                }), CancellationToken.None);

            Assert.True(response.Success);
            CutFillReport? report = response.Data?.Deserialize<CutFillReport>(SharedJson.Options);
            Assert.NotNull(report);
            Assert.Equal("Not Supported", report!.Summary.Verdict);
            Assert.Equal("Read-only volumes are unavailable.", report.Summary.NotSupportedReason);
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
    public void Discovery_FindsCalculateCutFillTool()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);

        Assert.True(catalog.TryGetTool("calculate_cut_fill", out ITool? tool));
        Assert.NotNull(tool);
        Assert.IsType<CalculateCutFillTool>(tool);
    }

    [Fact]
    public void Manifest_GeneratesCalculateCutFillTool()
    {
        Container container = CreateContainer();
        var generator = new ManifestGenerator();

        Autodesk.Mcp.Shared.Dtos.ToolManifest manifest = generator.Generate(typeof(CalculateCutFillTool));

        Assert.Equal("calculate_cut_fill", manifest.Name);
        Assert.Equal(Autodesk.Mcp.Shared.Enums.ToolCategory.Surfaces, manifest.Category);
        Assert.Equal(Autodesk.Mcp.Shared.Enums.ToolPermission.ReadOnly, manifest.Permission);
    }
}
