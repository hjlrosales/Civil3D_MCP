using System.Text.Json;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Errors;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Bridge.Execution;
using Civil3D.Tools.Project.Dtos;
using Civil3D.Tools.Project.Tools;
using Xunit;
using static Civil3D.Tools.Project.Tests.ProjectHarness;
using static Civil3D.Tools.Project.Tests.TestDoubles;

namespace Civil3D.Tools.Project.Tests;

/// <summary>
/// End-to-end through the real SDK dispatcher: tool discovery, manifest generation, request
/// routing, the <c>project_summary_report</c> tool, the workflow dispatcher pipeline and the
/// protocol response envelope (success, no-active-document, unknown-tool).
/// </summary>
public class ProjectSummaryReportToolTests
{
    [Fact]
    public async Task Dispatch_ProjectSummary_ReturnsSuccessEnvelopeWithReport()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);
        ToolDispatcher dispatcher = CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(
                Invoke("project_summary_report"), CancellationToken.None);

            Assert.True(response.Success);
            Assert.NotNull(response.Data);
            ProjectSummaryReport? report = response.Data.Value.Deserialize<ProjectSummaryReport>(SharedJson.Options);
            Assert.NotNull(report);
            Assert.Equal("ProjectSample.dwg", report!.Overview.DrawingName);
            Assert.Equal("project.summary.report", report.Execution.WorkflowName);
            Assert.Equal(5, report.Execution.TotalSteps);
            Assert.Equal(2, report.Inventory.AlignmentCount);
            Assert.Contains(report.Recommendations, r => r.Title == "Review unused styles");
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
                Invoke("project_summary_report"), CancellationToken.None);

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
    public void Discovery_FindsProjectSummaryTool()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);

        Assert.True(catalog.TryGetTool("project_summary_report", out ITool? tool));
        Assert.NotNull(tool);
        Assert.IsType<ProjectSummaryReportTool>(tool);
    }

    [Fact]
    public void Manifest_GeneratesProjectSummaryTool()
    {
        Container container = CreateContainer();
        var generator = new ManifestGenerator();

        Autodesk.Mcp.Shared.Dtos.ToolManifest manifest = generator.Generate(typeof(ProjectSummaryReportTool));

        Assert.Equal("project_summary_report", manifest.Name);
        Assert.Equal(Autodesk.Mcp.Shared.Enums.ToolCategory.Drawing, manifest.Category);
        Assert.Equal(Autodesk.Mcp.Shared.Enums.ToolPermission.ReadOnly, manifest.Permission);
    }
}
