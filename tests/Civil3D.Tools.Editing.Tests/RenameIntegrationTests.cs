using System.Text.Json;
using Autodesk.Mcp.Sdk.Dispatch;
using Autodesk.Mcp.Sdk.Hosting;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Errors;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Bridge.Execution;
using Civil3D.Domain.Commands;
using Civil3D.Tools.Editing.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static Civil3D.Tools.Editing.Tests.EditingTestHarness;

namespace Civil3D.Tools.Editing.Tests;

/// <summary>
/// End-to-end through the real SDK dispatcher: tool discovery, manifest generation, request
/// routing, the rename tool, the command dispatcher pipeline, the write transaction
/// (commit and rollback) and the protocol response envelope — with mocked Autodesk services.
/// </summary>
public class RenameIntegrationTests
{
    private sealed class InlineContext : IApplicationContext
    {
        public Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken) => action();
    }

    private static ToolDispatcher CreateDispatcher(Container container)
    {
        var dispatcher = new ToolDispatcher(
            CreateCatalog(container),
            new InlineContext(),
            new CancellationRegistry(),
            NullLogger<ToolDispatcher>.Instance);
        dispatcher.Start();
        return dispatcher;
    }

    private static ToolInvocation Invoke(string tool, object parameters) => new()
    {
        ToolName = tool,
        Parameters = JsonSerializer.SerializeToElement(parameters, SharedJson.Options),
        CorrelationId = "c-integ",
        SessionId = "s-integ",
        TimeoutMilliseconds = 10_000,
    };

    [Fact]
    public async Task Discovery_ExposesBothRenameTools()
    {
        Container c = Create();
        var catalog = CreateCatalog(c);

        string[] names = catalog.ToolNames.ToArray();

        Assert.Contains("rename_alignment", names);
        Assert.Contains("rename_surface", names);
        Autodesk.Mcp.Shared.Dtos.ToolManifest manifest = catalog.Manifests.Single(m => m.Name == "rename_alignment");
        Assert.Equal(Autodesk.Mcp.Shared.Enums.ToolPermission.ModifyDrawing, manifest.Permission);
    }

    [Fact]
    public async Task Dispatch_RenameAlignment_CommitsThroughProtocolEnvelope()
    {
        Container c = Create();
        ToolDispatcher dispatcher = CreateDispatcher(c);

        ResponseEnvelope response = await dispatcher.ExecuteAsync(
            Invoke("rename_alignment", new { ObjectId = 1, NewName = "Through Pipe" }),
            CancellationToken.None);

        Assert.True(response.Success, response.Message);
        Assert.Equal("c-integ", response.CorrelationId);
        Assert.NotNull(response.Data);
        Assert.Equal("Through Pipe", c.Drawing.FindAlignment(1)!.Name);
        Assert.Contains(c.Events.Published, e => e is ObjectRenamed { ObjectId: 1, NewName: "Through Pipe" });
        Assert.Contains(c.Events.Published, e => e is TransactionCommitted);
    }

    [Fact]
    public async Task Dispatch_RenameSurface_RollsBackOnMissingObject()
    {
        Container c = Create();
        ToolDispatcher dispatcher = CreateDispatcher(c);

        ResponseEnvelope response = await dispatcher.ExecuteAsync(
            Invoke("rename_surface", new { ObjectId = 404, NewName = "Ghost" }),
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.E_OBJECT_NOT_FOUND, response.ErrorCode);
        Assert.Equal("EG", c.Drawing.FindSurface(10)!.Name); // unchanged
    }

    [Fact]
    public async Task Dispatch_RenameAlignment_DuplicateName_ReturnsValidationFailed()
    {
        Container c = Create();
        ToolDispatcher dispatcher = CreateDispatcher(c);

        ResponseEnvelope response = await dispatcher.ExecuteAsync(
            Invoke("rename_alignment", new { ObjectId = 1, NewName = "Ramp A" }),
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, response.ErrorCode);
        Assert.Equal("Mainline", c.Drawing.FindAlignment(1)!.Name);
    }

    [Fact]
    public async Task Dispatch_ValidationFailure_IsStructural()
    {
        // An invalid name is rejected by the validators before any transaction is begun.
        Container c = Create();
        ToolDispatcher dispatcher = CreateDispatcher(c);

        ResponseEnvelope response = await dispatcher.ExecuteAsync(
            Invoke("rename_alignment", new { ObjectId = 1, NewName = "bad/name" }),
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, response.ErrorCode);
        Assert.Equal("Mainline", c.Drawing.FindAlignment(1)!.Name);
    }
}
