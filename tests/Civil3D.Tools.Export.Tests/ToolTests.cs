using System.Text.Json;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Errors;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Bridge.Execution;
using Civil3D.Tools.Export.Abstractions;
using Civil3D.Tools.Export.Dtos;
using Civil3D.Tools.Export.Tools;
using Xunit;
using static Civil3D.Tools.Export.Tests.ExportHarness;
using static Civil3D.Tools.Export.Tests.TestDoubles;

namespace Civil3D.Tools.Export.Tests;

/// <summary>
/// End-to-end through the real SDK dispatcher: tool discovery, manifest generation, request
/// routing, typed-parameter binding for <c>export_landxml</c>, the workflow pipeline and the
/// protocol response envelope (success, not-supported, invalid parameters, no active document,
/// unknown tool).
/// </summary>
public class ExportLandXmlToolTests
{
    [Fact]
    public async Task Dispatch_ExportLandXml_ReturnsSuccessEnvelopeWithReport()
    {
        string path = SampleData.TempXmlPath();
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);
        ToolDispatcher dispatcher = CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(
                Invoke("export_landxml", new LandXmlExportRequest { OutputPath = path }),
                CancellationToken.None);

            Assert.True(response.Success);
            Assert.NotNull(response.Data);
            LandXmlExportReport? report =
                response.Data?.Deserialize<LandXmlExportReport>(SharedJson.Options);
            Assert.NotNull(report);
            Assert.Equal("Exported", report!.Summary.Status);
            Assert.Equal(path, report.Summary.OutputPath);
            Assert.True(report.Summary.FileSizeBytes > 0);
            Assert.Equal(6, report.Statistics.TotalCollected);
            Assert.Equal("landxml.export", report.Execution.WorkflowName);
            Assert.True(File.Exists(path));
        }
        finally
        {
            await dispatcher.DisposeAsync();
            TryDelete(path);
        }
    }

    [Fact]
    public async Task Dispatch_NotSupported_ReturnsStructuredReportEnvelope()
    {
        string path = SampleData.TempXmlPath();
        Container container = CreateContainer(exporter: new FakeLandXmlExporter(
            status: LandXmlExportStatus.NotSupported,
            reason: "Not available in the read-only workflow layer."));
        ToolCatalog catalog = CreateCatalog(container);
        ToolDispatcher dispatcher = CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(
                Invoke("export_landxml", new LandXmlExportRequest { OutputPath = path }),
                CancellationToken.None);

            Assert.True(response.Success);
            LandXmlExportReport? report =
                response.Data?.Deserialize<LandXmlExportReport>(SharedJson.Options);
            Assert.NotNull(report);
            Assert.Equal("Not Supported", report!.Summary.Status);
            Assert.Equal("Not available in the read-only workflow layer.", report.Summary.NotSupportedReason);
            Assert.False(File.Exists(path));
        }
        finally
        {
            await dispatcher.DisposeAsync();
            TryDelete(path);
        }
    }

    [Fact]
    public async Task Dispatch_EmptyOutputPath_ReturnsInvalidParametersEnvelope()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);
        ToolDispatcher dispatcher = CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(
                Invoke("export_landxml", new LandXmlExportRequest { OutputPath = "" }),
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
    public async Task Dispatch_FileExistsWithoutOverwrite_ReturnsInvalidParametersEnvelope()
    {
        string path = SampleData.TempXmlPath();
        File.WriteAllText(path, "existing");
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);
        ToolDispatcher dispatcher = CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(
                Invoke("export_landxml", new LandXmlExportRequest { OutputPath = path }),
                CancellationToken.None);

            Assert.False(response.Success);
            Assert.Equal(ErrorCode.E_INVALID_PARAMETERS, response.ErrorCode);
        }
        finally
        {
            await dispatcher.DisposeAsync();
            TryDelete(path);
        }
    }

    [Fact]
    public async Task Dispatch_NoActiveDocument_ReturnsNoActiveDocumentEnvelope()
    {
        string path = SampleData.TempXmlPath();
        Container container = CreateContainer(session: new FakeSession(drawing: null));
        ToolCatalog catalog = CreateCatalog(container);
        ToolDispatcher dispatcher = CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(
                Invoke("export_landxml", new LandXmlExportRequest { OutputPath = path }),
                CancellationToken.None);

            Assert.False(response.Success);
            Assert.Equal(ErrorCode.E_NO_ACTIVE_DOCUMENT, response.ErrorCode);
            Assert.Null(response.Data);
        }
        finally
        {
            await dispatcher.DisposeAsync();
            TryDelete(path);
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
    public void Discovery_FindsExportLandXmlTool()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);

        Assert.True(catalog.TryGetTool("export_landxml", out ITool? tool));
        Assert.NotNull(tool);
        Assert.IsType<ExportLandXmlTool>(tool);
    }

    [Fact]
    public void Manifest_GeneratesExportLandXmlTool()
    {
        Container container = CreateContainer();
        var generator = new ManifestGenerator();

        Autodesk.Mcp.Shared.Dtos.ToolManifest manifest = generator.Generate(typeof(ExportLandXmlTool));

        Assert.Equal("export_landxml", manifest.Name);
        Assert.Equal(Autodesk.Mcp.Shared.Enums.ToolCategory.Export, manifest.Category);
        Assert.Equal(Autodesk.Mcp.Shared.Enums.ToolPermission.Export, manifest.Permission);
    }

    private static void TryDelete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
