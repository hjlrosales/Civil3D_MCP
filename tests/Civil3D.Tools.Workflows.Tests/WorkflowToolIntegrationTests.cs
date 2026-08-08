using System.Text.Json;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Errors;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Bridge.Execution;
using Xunit;
using static Civil3D.Tools.Workflows.Tests.TestDoubles;
using static Civil3D.Tools.Workflows.Tests.TestTools;
using static Civil3D.Tools.Workflows.Tests.WorkflowToolHarness;

namespace Civil3D.Tools.Workflows.Tests;

/// <summary>
/// End-to-end through the real SDK dispatcher: tool discovery, manifest generation, request
/// routing, the workflow tool, the workflow dispatcher pipeline (validation, permission,
/// timeout/cancellation, progress, events) and the protocol response envelope.
/// </summary>
public class WorkflowToolIntegrationTests
{
    [Fact]
    public async Task Dispatch_ReportWorkflow_ReturnsSuccessEnvelope()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);
        ToolDispatcher dispatcher = TestDoubles.CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(
                Invoke("test_report", new { value = "hello" }), CancellationToken.None);

            Assert.True(response.Success);
            Assert.NotNull(response.Data);
            ReportResult? data = response.Data.Value.Deserialize<ReportResult>(SharedJson.Options);
            Assert.Equal("done", data!.Value);
            Assert.Equal(["ran"], container.Store.Entries);
        }
        finally
        {
            await dispatcher.DisposeAsync();
        }
    }

    [Fact]
    public async Task Dispatch_Timeout_ReturnsTimeoutEnvelope()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);
        ToolDispatcher dispatcher = TestDoubles.CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(
                Invoke("test_timeout"), CancellationToken.None);

            Assert.False(response.Success);
            Assert.Equal(ErrorCode.E_TIMEOUT, response.ErrorCode);
            Assert.Null(response.Data);
        }
        finally
        {
            await dispatcher.DisposeAsync();
        }
    }

    [Fact]
    public async Task Dispatch_ValidationFailure_ReturnsValidationEnvelope()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);
        ToolDispatcher dispatcher = TestDoubles.CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(
                Invoke("test_report", new { value = (string?)null }), CancellationToken.None);

            Assert.False(response.Success);
            Assert.Equal(ErrorCode.E_VALIDATION_FAILED, response.ErrorCode);
            Assert.Empty(container.Store.Entries);
        }
        finally
        {
            await dispatcher.DisposeAsync();
        }
    }

    [Fact]
    public async Task Dispatch_DomainFailure_ReturnsObjectNotFoundEnvelope()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);
        ToolDispatcher dispatcher = TestDoubles.CreateDispatcher(catalog);
        try
        {
            ResponseEnvelope response = await dispatcher.ExecuteAsync(
                Invoke("test_domainfail"), CancellationToken.None);

            Assert.False(response.Success);
            Assert.Equal(ErrorCode.E_OBJECT_NOT_FOUND, response.ErrorCode);
        }
        finally
        {
            await dispatcher.DisposeAsync();
        }
    }

    [Fact]
    public async Task Manifest_IncludesWorkflowTools()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);

        var generator = new ManifestGenerator();

        Assert.Equal("test_report", generator.Generate(typeof(ReportWorkflowTool)).Name);
        Assert.Equal("test_timeout", generator.Generate(typeof(TimeoutWorkflowTool)).Name);
    }
}
