using System.Text.Json;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Errors;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Domain.Commands;
using Civil3D.Tools.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Civil3D.Tools.Commands.Tests.TestCommands;
using static Civil3D.Tools.Commands.Tests.TestDoubles;
using static Civil3D.Tools.Commands.Tests.CommandHarness;

namespace Civil3D.Tools.Commands.Tests;

/// <summary>
/// The command tool base in isolation: parameter binding, the full dispatcher pipeline and the
/// mapping of command/domain failures to protocol error codes (E_VALIDATION_FAILED,
/// E_PERMISSION_DENIED, E_CONFIRMATION_REQUIRED, E_NO_ACTIVE_DOCUMENT, E_TRANSACTION_FAILED).
/// </summary>
public class CommandToolBaseTests
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

    /// <summary>Constructs a command tool with the harness's real dispatcher and gate.</summary>
    private static TTool BuildTool<TTool>(Container container, IConfirmationGate? gate = null)
        where TTool : class
        => (TTool)Activator.CreateInstance(
            typeof(TTool),
            container.Provider.GetRequiredService<ICivil3DSession>(),
            container.Provider.GetRequiredService<ICommandDispatcher>(),
            gate ?? container.Provider.GetRequiredService<IConfirmationGate>(),
            container.Provider.GetRequiredService<IUndoContext>())!;

    [Fact]
    public async Task ValidCommand_CommitsTransaction_AndReturnsResult()
    {
        Container container = CreateContainer();
        var tool = BuildTool<RecordLogTool>(container);

        object? result = await ExecuteToolAsync(tool, new { label = "hello" });

        RecordLogResult typed = Assert.IsType<RecordLogResult>(result);
        Assert.Equal("hello", typed.Label);
        Assert.True(typed.HadTransaction);
        Assert.Equal(["hello"], container.Repository.Entries);
        Assert.True(Assert.Single(container.Transactions.Begun).IsCommitted);
    }

    [Fact]
    public async Task ValidationFailure_MapsToValidationFailed()
    {
        Container container = CreateContainer();
        var tool = BuildTool<RecordLogTool>(container);

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => ExecuteToolAsync(tool, new { label = "  " }));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
        Assert.Empty(container.Repository.Entries);
        Assert.Empty(container.Transactions.Begun);
    }

    [Fact]
    public async Task PermissionDenied_MapsToPermissionDenied()
    {
        Container container = CreateContainer();
        var tool = BuildTool<DeniedRecordLogTool>(container);

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => ExecuteToolAsync(tool, new { label = "hello" }));

        Assert.Equal(ErrorCode.E_PERMISSION_DENIED, ex.ErrorCode);
        Assert.Empty(container.Repository.Entries);
        Assert.Empty(container.Transactions.Begun);
    }

    [Fact]
    public async Task ConfirmationRequired_NotGranted_MapsToConfirmationRequired()
    {
        Container container = CreateContainer();
        var tool = BuildTool<DestructiveTool>(container);

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(() => ExecuteToolAsync(tool));

        Assert.Equal(ErrorCode.E_CONFIRMATION_REQUIRED, ex.ErrorCode);
        Assert.Empty(container.Repository.Entries);
        Assert.Empty(container.Transactions.Begun);
    }

    [Fact]
    public async Task ConfirmationGranted_Executes()
    {
        Container container = CreateContainer(confirmationGate: new GrantingConfirmationGate());
        var tool = BuildTool<DestructiveTool>(container);

        object? result = await ExecuteToolAsync(tool, new { label = "delete-layer" });

        RecordLogResult typed = Assert.IsType<RecordLogResult>(result);
        Assert.Equal("delete-layer", typed.Label);
        Assert.Equal(["delete-layer"], container.Repository.Entries);
    }

    [Fact]
    public async Task WithoutActiveDocument_MapsToNoActiveDocument()
    {
        Container container = CreateContainer(session: new FakeSession(drawing: null));
        var tool = BuildTool<RecordLogTool>(container);

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => ExecuteToolAsync(tool, new { label = "hello" }));

        Assert.Equal(ErrorCode.E_NO_ACTIVE_DOCUMENT, ex.ErrorCode);
    }

    [Fact]
    public async Task HandlerDomainFailure_RollsBackAndMapsToTransactionFailed()
    {
        Container container = CreateContainer();
        var tool = BuildTool<FailingTool>(container);

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(() => ExecuteToolAsync(tool));

        Assert.Equal(ErrorCode.E_TRANSACTION_FAILED, ex.ErrorCode);
        Assert.True(Assert.Single(container.Transactions.Begun).IsRolledBack);
        Assert.Single(container.Events.Published.OfType<TransactionRolledBack>());
    }
}
