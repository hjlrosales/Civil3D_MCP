using System.Text.Json;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Errors;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Domain.Commands;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Civil3D.Tools.Workflows.Tests.TestDoubles;
using static Civil3D.Tools.Workflows.Tests.TestTools;
using static Civil3D.Tools.Workflows.Tests.WorkflowToolHarness;

namespace Civil3D.Tools.Workflows.Tests;

/// <summary>
/// The workflow tool base in isolation: parameter binding, the full dispatcher pipeline and the
/// mapping of workflow/domain failures to protocol error codes (E_VALIDATION_FAILED,
/// E_PERMISSION_DENIED, E_NO_ACTIVE_DOCUMENT, E_TIMEOUT, E_OBJECT_NOT_FOUND, E_INTERNAL).
/// </summary>
public class WorkflowToolBaseTests
{
    private static async Task<object?> ExecuteToolAsync(ITool tool, object? parameters = null)
    {
        var context = new ToolExecutionContext
        {
            ToolName = tool.Name,
            CorrelationId = "c-1",
            SessionId = "s-1",
            CancellationToken = CancellationToken.None,
        };
        JsonElement? json = parameters is null
            ? null
            : JsonSerializer.SerializeToElement(parameters, SharedJson.Options);
        return await tool.ExecuteAsync(context, json);
    }

    /// <summary>Constructs a workflow tool with the harness's real dispatcher and container.</summary>
    private static TTool BuildTool<TTool>(Container container)
        where TTool : class
        => (TTool)Activator.CreateInstance(
            typeof(TTool),
            container.Provider.GetRequiredService<ICivil3DSession>(),
            container.Provider.GetRequiredService<IWorkflowDispatcher>(),
            container.Provider)!;

    [Fact]
    public async Task ValidWorkflow_RunsStepsAndReturnsResult()
    {
        Container container = CreateContainer();
        var tool = BuildTool<ReportWorkflowTool>(container);

        object? result = await ExecuteToolAsync(tool, new { value = "hello" });

        ReportResult typed = Assert.IsType<ReportResult>(result);
        Assert.Equal("done", typed.Value);
        Assert.Equal(1, typed.StepCount);
        Assert.Equal(["ran"], container.Store.Entries);
        Assert.Single(container.Events.Published.OfType<WorkflowCompleted>());
    }

    [Fact]
    public async Task ValidationFailure_MapsToValidationFailed()
    {
        Container container = CreateContainer();
        var tool = BuildTool<ReportWorkflowTool>(container);

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => ExecuteToolAsync(tool, new { value = (string?)null }));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
        Assert.Empty(container.Store.Entries);
        Assert.Single(container.Events.Published.OfType<WorkflowFailed>());
    }

    [Fact]
    public async Task PermissionDenied_MapsToPermissionDenied()
    {
        Container container = CreateContainer();
        var tool = BuildTool<DeniedWorkflowTool>(container);

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => ExecuteToolAsync(tool, new { value = "hello" }));

        Assert.Equal(ErrorCode.E_PERMISSION_DENIED, ex.ErrorCode);
        Assert.Empty(container.Store.Entries);
    }

    [Fact]
    public async Task WithoutActiveDocument_MapsToNoActiveDocument()
    {
        Container container = CreateContainer(session: new FakeSession(drawing: null));
        var tool = BuildTool<ReportWorkflowTool>(container);

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => ExecuteToolAsync(tool, new { value = "hello" }));

        Assert.Equal(ErrorCode.E_NO_ACTIVE_DOCUMENT, ex.ErrorCode);
    }

    [Fact]
    public async Task StepFailure_MapsToInternal()
    {
        Container container = CreateContainer();
        var tool = BuildTool<FailingWorkflowTool>(container);

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(() => ExecuteToolAsync(tool));

        Assert.Equal(ErrorCode.E_INTERNAL, ex.ErrorCode);
        Assert.Single(container.Events.Published.OfType<WorkflowFailed>());
    }

    [Fact]
    public async Task DomainFailure_MapsToObjectNotFound()
    {
        Container container = CreateContainer();
        var tool = BuildTool<DomainFailWorkflowTool>(container);

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(() => ExecuteToolAsync(tool));

        Assert.Equal(ErrorCode.E_OBJECT_NOT_FOUND, ex.ErrorCode);
    }

    [Fact]
    public async Task Timeout_MapsToTimeout()
    {
        Container container = CreateContainer();
        var tool = BuildTool<TimeoutWorkflowTool>(container);

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(() => ExecuteToolAsync(tool));

        Assert.Equal(ErrorCode.E_TIMEOUT, ex.ErrorCode);
        WorkflowFailed failed = Assert.Single(container.Events.Published.OfType<WorkflowFailed>());
        Assert.Equal("Timeout", failed.ErrorCode);
    }
}
