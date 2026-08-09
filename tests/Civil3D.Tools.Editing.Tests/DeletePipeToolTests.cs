using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Errors;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Tools.Editing.Dtos;
using Xunit;
using static Civil3D.Tools.Editing.Tests.EditingTestHarness;

namespace Civil3D.Tools.Editing.Tests;

/// <summary>
/// The <c>delete_pipe</c> tool through the full command pipeline (validation → confirmation →
/// write transaction → commit/rollback → domain events → protocol response): success, unknown
/// pipe ids, structural validation, the best-effort drawing save after a successful delete, and
/// confirmation gating.
/// </summary>
public class DeletePipeToolTests
{
    private static async Task<CreatePipeResult> CreatePipeAsync(Container c)
    {
        var context = new ToolExecutionContext
        {
            ToolName = "create_pipe",
            CorrelationId = "c-1",
            SessionId = "s-1",
        };
        var request = new CreatePipeRequest
        {
            NetworkName = "Storm",
            Material = "HDPE",
            Sdr = "17",
            PressureClassBar = 10,
            DiameterMm = 200,
            LengthMeters = 10,
            StartEasting = 1000,
            StartNorthing = 2000,
            StartElevation = 95.5,
        };
        var parameters = System.Text.Json.JsonSerializer.SerializeToElement(
            request, Autodesk.Mcp.Shared.Serialization.SharedJson.Options);
        return (CreatePipeResult)(await c.CreatePipeTool.ExecuteAsync(context, parameters))!;
    }

    private static async Task<DeletePipeResult> DeletePipeAsync(Container c, DeletePipeRequest request)
    {
        var context = new ToolExecutionContext
        {
            ToolName = "delete_pipe",
            CorrelationId = "c-1",
            SessionId = "s-1",
        };
        var parameters = System.Text.Json.JsonSerializer.SerializeToElement(
            request, Autodesk.Mcp.Shared.Serialization.SharedJson.Options);
        return (DeletePipeResult)(await c.DeletePipeTool.ExecuteAsync(context, parameters))!;
    }

    /// <summary>Adds a pipe directly to the default drawing's "Storm" network (bypasses the
    /// confirmation-gated create tool, which would be denied in confirmation tests).</summary>
    private static long SeedPipe(Container c)
    {
        InMemoryDrawing.FakeNetwork network = c.Drawing.Networks.Single(n => n.Name == "Storm");
        InMemoryDrawing.FakePartFamily family = network.PartFamilies[0];
        long pipeId = c.Drawing.NextPipeId();
        var pipe = new InMemoryDrawing.FakePipe(pipeId, $"Pipe-{pipeId}", family.Description, family.Sizes[0]);
        network.Pipes.Add(pipe);
        return pipeId;
    }

    [Fact]
    public async Task DeletePipe_Succeeds_RemovesPipeCommitsAndRaisesEvents()
    {
        Container c = Create();
        CreatePipeResult created = await CreatePipeAsync(c);

        DeletePipeResult result = await DeletePipeAsync(c, new DeletePipeRequest { PipeId = created.PipeId });

        Assert.True(result.Success);
        Assert.Equal(created.PipeId, result.PipeId);
        Assert.Equal("Storm", result.NetworkName);
        Assert.Equal("HDPE SDR17 PN10 Pipe", result.PartFamilyName);
        Assert.Contains(c.Events.Published, e => e is Civil3D.Domain.Commands.PartDeleted p
            && p.PartType == "pipe" && p.PartId == created.PipeId && p.NetworkId == 100);
        Assert.Contains(c.Events.Published, e => e is Civil3D.Domain.Commands.TransactionCommitted);
        Assert.Empty(c.Drawing.Networks.Single(n => n.Name == "Storm").Pipes);
        Assert.Equal(2, c.Undo.Units.Count); // one for the create, one for the delete
    }

    [Fact]
    public async Task DeletePipe_UnknownPipeId_MapsToObjectNotFound()
    {
        Container c = Create();
        CreatePipeResult created = await CreatePipeAsync(c);

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => DeletePipeAsync(c, new DeletePipeRequest { PipeId = 999_999 }));

        Assert.Equal(ErrorCode.E_OBJECT_NOT_FOUND, ex.ErrorCode);
        Assert.Single(c.Drawing.Networks.Single(n => n.Name == "Storm").Pipes); // unchanged
        Assert.DoesNotContain(c.Events.Published, e => e is Civil3D.Domain.Commands.PartDeleted);
    }

    [Fact]
    public async Task DeletePipe_NonPositiveId_MapsToValidationFailed()
    {
        Container c = Create();

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => DeletePipeAsync(c, new DeletePipeRequest { PipeId = 0 }));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
    }

    [Fact]
    public async Task DeletePipe_SavesDrawingAfterSuccessfulDelete()
    {
        Container c = Create();
        CreatePipeResult created = await CreatePipeAsync(c);

        DeletePipeResult result = await DeletePipeAsync(c, new DeletePipeRequest { PipeId = created.PipeId });

        Assert.True(result.Success);
        Assert.Equal(1, c.SaveService.SaveCount);
        Assert.False(c.SaveService.LastZoomToExtents); // delete saves without zooming
    }

    [Fact]
    public async Task DeletePipe_DoesNotSaveWhenDeleteFails()
    {
        Container c = Create();

        await Assert.ThrowsAsync<BridgeException>(
            () => DeletePipeAsync(c, new DeletePipeRequest { PipeId = 999_999 }));

        Assert.Equal(0, c.SaveService.SaveCount);
    }

    [Fact]
    public async Task DeletePipe_SaveFailure_DoesNotFailTheDelete()
    {
        Container c = Create();
        c.SaveService.Failure = new InvalidOperationException("disk full");
        CreatePipeResult created = await CreatePipeAsync(c);

        DeletePipeResult result = await DeletePipeAsync(c, new DeletePipeRequest { PipeId = created.PipeId });

        Assert.True(result.Success); // best-effort save never fails a successful delete
        Assert.Empty(c.Drawing.Networks.Single(n => n.Name == "Storm").Pipes);
    }

    [Fact]
    public async Task DeletePipe_ConfirmationRequired_AndDenied()
    {
        Container c = Create(requireConfirmation: true); // NullConfirmationGate denies
        long pipeId = SeedPipe(c);

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => DeletePipeAsync(c, new DeletePipeRequest { PipeId = pipeId }));

        Assert.Equal(ErrorCode.E_CONFIRMATION_REQUIRED, ex.ErrorCode);
        Assert.Single(c.Drawing.Networks.Single(n => n.Name == "Storm").Pipes); // unchanged
        Assert.Equal(0, c.SaveService.SaveCount);
    }

    [Fact]
    public async Task DeletePipe_ConfirmationRequired_AndGranted()
    {
        Container c = Create(requireConfirmation: true, confirmationGate: new GrantingConfirmationGate());
        long pipeId = SeedPipe(c);

        DeletePipeResult result = await DeletePipeAsync(c, new DeletePipeRequest { PipeId = pipeId });

        Assert.True(result.Success);
        Assert.Empty(c.Drawing.Networks.Single(n => n.Name == "Storm").Pipes);
        Assert.Equal(1, c.SaveService.SaveCount);
    }

    [Fact]
    public async Task DeletePipeResult_SerializesWithAllFields()
    {
        Container c = Create();
        CreatePipeResult created = await CreatePipeAsync(c);
        DeletePipeResult result = await DeletePipeAsync(c, new DeletePipeRequest { PipeId = created.PipeId });

        string json = System.Text.Json.JsonSerializer.Serialize(result, Autodesk.Mcp.Shared.Serialization.SharedJson.Options);

        Assert.Contains("\"pipeId\"", json);
        Assert.Contains("\"networkName\":\"Storm\"", json);
        Assert.Contains("\"success\":true", json);
        Assert.Contains("\"deletedAtUtc\"", json);
    }
}
