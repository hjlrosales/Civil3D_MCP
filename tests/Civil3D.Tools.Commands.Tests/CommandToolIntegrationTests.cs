using System.Text.Json;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Errors;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Bridge.Execution;
using Civil3D.Domain.Commands;
using Xunit;
using static Civil3D.Tools.Commands.Tests.TestCommands;
using static Civil3D.Tools.Commands.Tests.TestDoubles;
using static Civil3D.Tools.Commands.Tests.CommandHarness;

namespace Civil3D.Tools.Commands.Tests;

/// <summary>
/// End-to-end integration through the real SDK dispatcher: tool discovery, manifest generation,
/// request routing, command tool, command dispatcher pipeline, write transaction (commit/rollback)
/// and the protocol response envelope — with mocked Autodesk services.
/// </summary>
public class CommandToolIntegrationTests
{
    private static ToolCatalog CreateCatalog(Container container) => CommandHarness.CreateCatalog(container);

    [Fact]
    public void Scanner_FindsCommandTestTools()
    {
        IReadOnlyList<Type> types = ToolScanner.FindToolTypes(new[] { typeof(RecordLogTool).Assembly });

        Assert.Contains(types, static t => t == typeof(RecordLogTool));
        Assert.Contains(types, static t => t == typeof(DeniedRecordLogTool));
        Assert.Contains(types, static t => t == typeof(DestructiveTool));
        Assert.Contains(types, static t => t == typeof(FailingTool));
    }

    [Fact]
    public void Catalog_ResolvesAndExposesCommandToolManifests()
    {
        Container container = CreateContainer();
        ToolCatalog catalog = CreateCatalog(container);

        Assert.True(catalog.TryGetTool("test_record_log", out ITool? tool));
        Assert.IsType<RecordLogTool>(tool);
        Assert.NotNull(catalog.GetManifest("test_record_log"));
        Assert.NotNull(catalog.GetManifest("test_destructive"));
    }

    [Fact]
    public async Task Execute_CommandToProtocolResponse_Commits()
    {
        Container container = CreateContainer();
        ToolDispatcher dispatcher = CreateDispatcher(CreateCatalog(container));

        ResponseEnvelope response = await dispatcher.ExecuteAsync(
            Invoke("test_record_log", new { label = "hello" }), CancellationToken.None);

        Assert.True(response.Success, response.Message);
        RecordLogResult? result = response.Data?.Deserialize<RecordLogResult>(SharedJson.Options);
        Assert.NotNull(result);
        Assert.Equal("hello", result.Label);
        Assert.Equal(["hello"], container.Repository.Entries);
        Assert.True(Assert.Single(container.Transactions.Begun).IsCommitted);
        Assert.Single(container.Events.Published.OfType<TransactionCommitted>());
    }

    [Fact]
    public async Task Execute_ValidationFailure_ReturnsValidationFailed()
    {
        Container container = CreateContainer();
        ToolDispatcher dispatcher = CreateDispatcher(CreateCatalog(container));

        ResponseEnvelope response = await dispatcher.ExecuteAsync(
            Invoke("test_record_log", new { label = "  " }), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, response.ErrorCode);
        Assert.Empty(container.Transactions.Begun);
    }

    [Fact]
    public async Task Execute_PermissionDenied_ReturnsPermissionDenied()
    {
        Container container = CreateContainer();
        ToolDispatcher dispatcher = CreateDispatcher(CreateCatalog(container));

        ResponseEnvelope response = await dispatcher.ExecuteAsync(
            Invoke("test_record_log_denied", new { label = "hello" }), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.E_PERMISSION_DENIED, response.ErrorCode);
    }

    [Fact]
    public async Task Execute_ConfirmationRequired_ReturnsConfirmationRequired()
    {
        Container container = CreateContainer();
        ToolDispatcher dispatcher = CreateDispatcher(CreateCatalog(container));

        ResponseEnvelope response = await dispatcher.ExecuteAsync(
            Invoke("test_destructive", new { label = "delete-layer" }), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.E_CONFIRMATION_REQUIRED, response.ErrorCode);
        Assert.Empty(container.Transactions.Begun);
    }

    [Fact]
    public async Task Execute_HandlerFailure_RollsBackAndReturnsTransactionFailed()
    {
        Container container = CreateContainer();
        ToolDispatcher dispatcher = CreateDispatcher(CreateCatalog(container));

        ResponseEnvelope response = await dispatcher.ExecuteAsync(
            Invoke("test_failing"), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.E_TRANSACTION_FAILED, response.ErrorCode);
        Assert.True(Assert.Single(container.Transactions.Begun).IsRolledBack);
        Assert.Single(container.Events.Published.OfType<TransactionRolledBack>());
    }
}
