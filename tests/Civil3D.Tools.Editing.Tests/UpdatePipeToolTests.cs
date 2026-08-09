using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Errors;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Tools.Editing.Dtos;
using Xunit;
using static Civil3D.Tools.Editing.Tests.EditingTestHarness;

namespace Civil3D.Tools.Editing.Tests;

/// <summary>
/// The <c>update_pipe</c> tool through the full command pipeline (validation -> confirmation ->
/// write transaction -> commit/rollback -> domain events -> protocol response): elevation, length
/// and diameter changes (individually and combined), structural validation failures, unknown pipe
/// ids, confirmation gating and undo registration.
/// </summary>
public class UpdatePipeToolTests
{
    private static async Task<CreatePipeResult> CreatePipeAsync(Container c, CreatePipeRequest request)
    {
        var context = new ToolExecutionContext { ToolName = "create_pipe", CorrelationId = "c-1", SessionId = "s-1" };
        var parameters = System.Text.Json.JsonSerializer.SerializeToElement(request, Autodesk.Mcp.Shared.Serialization.SharedJson.Options);
        return (CreatePipeResult)(await c.CreatePipeTool.ExecuteAsync(context, parameters))!;
    }

    private static async Task<UpdatePipeResult> UpdatePipeAsync(Container c, UpdatePipeRequest request)
    {
        var context = new ToolExecutionContext { ToolName = "update_pipe", CorrelationId = "c-2", SessionId = "s-2" };
        var parameters = System.Text.Json.JsonSerializer.SerializeToElement(request, Autodesk.Mcp.Shared.Serialization.SharedJson.Options);
        return (UpdatePipeResult)(await c.UpdatePipeTool.ExecuteAsync(context, parameters))!;
    }

    private static CreatePipeRequest HdpeRequest(double directionDegrees = 0) => new()
    {
        NetworkName = "Storm",
        Material = "HDPE",
        Sdr = "17",
        PressureClassBar = 10,
        DiameterMm = 200,
        LengthMeters = 10,
        DirectionDegrees = directionDegrees,
        StartEasting = 1000,
        StartNorthing = 2000,
        StartElevation = 95.5,
    };

    [Fact]
    public async Task UpdatePipe_ElevationOnly_SetsBothEndsAndRaisesEvent()
    {
        Container c = Create();
        CreatePipeResult created = await CreatePipeAsync(c, HdpeRequest());

        UpdatePipeResult result = await UpdatePipeAsync(c, new UpdatePipeRequest
        {
            PipeId = created.PipeId,
            ElevationMeters = 98.25,
        });

        Assert.True(result.Success);
        Assert.Equal(new[] { "elevation" }, result.ChangesApplied);
        Assert.Equal(98.25, result.StartElevation, precision: 6);
        Assert.Equal(98.25, result.EndElevation, precision: 6);
        Assert.Equal(created.Length3D, result.Length3D, precision: 6);
        InMemoryDrawing.FakePipe pipe = c.Drawing.FindPipe(created.PipeId)!;
        Assert.Equal(98.25, pipe.StartElevation, precision: 6);
        Assert.Equal(98.25, pipe.EndElevation, precision: 6);
        Assert.Contains(c.Events.Published, e => e is Civil3D.Domain.Commands.PartUpdated { PartType: "pipe" });
        Assert.Contains(c.Events.Published, e => e is Civil3D.Domain.Commands.TransactionCommitted);
        Assert.Equal(2, c.Undo.Units.Count); // one undo unit per command
    }

    [Fact]
    public async Task UpdatePipe_LengthOnly_MovesEndAlongCurrentBearing()
    {
        Container c = Create();
        // Pipe runs +Northing (direction 90): end (1000, 2010).
        CreatePipeResult created = await CreatePipeAsync(c, HdpeRequest(directionDegrees: 90));

        UpdatePipeResult result = await UpdatePipeAsync(c, new UpdatePipeRequest
        {
            PipeId = created.PipeId,
            LengthMeters = 15,
        });

        Assert.True(result.Success);
        Assert.Equal(new[] { "length" }, result.ChangesApplied);
        Assert.Equal(1000, result.StartEasting, precision: 6); // start fixed
        Assert.Equal(2000, result.StartNorthing, precision: 6);
        Assert.Equal(1000, result.EndEasting, precision: 6); // end moved along +Northing
        Assert.Equal(2015, result.EndNorthing, precision: 6);
        Assert.Equal(95.5, result.EndElevation, precision: 6); // end elevation preserved
        Assert.Equal(15, result.Length3D, precision: 6);
    }

    [Fact]
    public async Task UpdatePipe_DiameterOnly_SnapsToClosestAvailableSize()
    {
        Container c = Create();
        CreatePipeResult created = await CreatePipeAsync(c, HdpeRequest()); // 200 mm

        UpdatePipeResult result = await UpdatePipeAsync(c, new UpdatePipeRequest
        {
            PipeId = created.PipeId,
            DiameterMm = 150,
        });

        Assert.True(result.Success);
        Assert.Equal(new[] { "diameter" }, result.ChangesApplied);
        Assert.Equal("150 mm", result.PartSizeName);
        Assert.Equal(0.15, result.InnerDiameterOrWidth, precision: 6);
        Assert.Equal(created.Length3D, result.Length3D, precision: 6); // geometry unchanged
    }

    [Fact]
    public async Task UpdatePipe_AllChangesAtOnce_AppliesInOrder()
    {
        Container c = Create();
        CreatePipeResult created = await CreatePipeAsync(c, HdpeRequest()); // 10 m, 200 mm, elev 95.5

        UpdatePipeResult result = await UpdatePipeAsync(c, new UpdatePipeRequest
        {
            PipeId = created.PipeId,
            ElevationMeters = 98,
            LengthMeters = 20,
            DiameterMm = 250,
        });

        Assert.True(result.Success);
        Assert.Equal(new[] { "elevation", "length", "diameter" }, result.ChangesApplied);
        Assert.Equal(98, result.StartElevation, precision: 6);
        Assert.Equal(98, result.EndElevation, precision: 6); // elevation applied before length
        Assert.Equal(1020, result.EndEasting, precision: 6); // length 20 along +Easting
        Assert.Equal(2000, result.EndNorthing, precision: 6);
        Assert.Equal(20, result.Length3D, precision: 6);
        Assert.Equal("250 mm", result.PartSizeName);
    }

    [Fact]
    public async Task UpdatePipe_NoChanges_MapsToValidationFailed()
    {
        Container c = Create();
        CreatePipeResult created = await CreatePipeAsync(c, HdpeRequest());

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => UpdatePipeAsync(c, new UpdatePipeRequest { PipeId = created.PipeId }));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
        Assert.Contains("At least one", ex.Message);
    }

    [Fact]
    public async Task UpdatePipe_UnknownPipeId_MapsToObjectNotFound()
    {
        Container c = Create();
        CreatePipeResult created = await CreatePipeAsync(c, HdpeRequest());

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => UpdatePipeAsync(c, new UpdatePipeRequest { PipeId = 999_999, LengthMeters = 12 }));

        Assert.Equal(ErrorCode.E_OBJECT_NOT_FOUND, ex.ErrorCode);
        InMemoryDrawing.FakePipe pipe = c.Drawing.FindPipe(created.PipeId)!;
        Assert.Equal(10, pipe.Length3D, precision: 6); // untouched
    }

    [Fact]
    public async Task UpdatePipe_NonPositiveLength_MapsToValidationFailed()
    {
        Container c = Create();
        CreatePipeResult created = await CreatePipeAsync(c, HdpeRequest());

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => UpdatePipeAsync(c, new UpdatePipeRequest { PipeId = created.PipeId, LengthMeters = 0 }));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
    }

    [Fact]
    public async Task UpdatePipe_NonPositiveDiameter_MapsToValidationFailed()
    {
        Container c = Create();
        CreatePipeResult created = await CreatePipeAsync(c, HdpeRequest());

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => UpdatePipeAsync(c, new UpdatePipeRequest { PipeId = created.PipeId, DiameterMm = -5 }));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
    }

    [Fact]
    public async Task UpdatePipe_NonPositivePipeId_MapsToValidationFailed()
    {
        Container c = Create();

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => UpdatePipeAsync(c, new UpdatePipeRequest { PipeId = 0, LengthMeters = 12 }));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
    }

    [Fact]
    public async Task UpdatePipe_ConfirmationRequired_AndDenied()
    {
        // Pre-populate the drawing with a pipe: with confirmation required, the create tool
        // would itself be denied, so the update tool is exercised against an existing pipe.
        var drawing = new InMemoryDrawing(
            networks:
            [
                new InMemoryDrawing.FakeNetwork(
                    100,
                    "Storm",
                    new InMemoryDrawing.FakePartFamily("HDPE SDR17 PN10 Pipe", 100, 150, 200, 250, 300)),
            ]);
        drawing.FindNetwork(100)!.Pipes.Add(new InMemoryDrawing.FakePipe(
            1000, "Pipe-1000", "HDPE SDR17 PN10 Pipe", new InMemoryDrawing.FakePartSize("200 mm", 200))
        {
            StartEasting = 1000,
            StartNorthing = 2000,
            StartElevation = 95.5,
            EndEasting = 1010,
            EndNorthing = 2000,
            EndElevation = 95.5,
            Length3D = 10,
        });
        Container c = Create(drawing: drawing, requireConfirmation: true); // NullConfirmationGate denies

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => UpdatePipeAsync(c, new UpdatePipeRequest { PipeId = 1000, ElevationMeters = 98 }));

        Assert.Equal(ErrorCode.E_CONFIRMATION_REQUIRED, ex.ErrorCode);
        InMemoryDrawing.FakePipe pipe = c.Drawing.FindPipe(1000)!;
        Assert.Equal(95.5, pipe.StartElevation, precision: 6); // unchanged
    }

    [Fact]
    public async Task UpdatePipe_ConfirmationRequired_AndGranted()
    {
        Container c = Create(requireConfirmation: true, confirmationGate: new GrantingConfirmationGate());
        CreatePipeResult created = await CreatePipeAsync(c, HdpeRequest());

        UpdatePipeResult result = await UpdatePipeAsync(c, new UpdatePipeRequest
        {
            PipeId = created.PipeId,
            ElevationMeters = 98,
        });

        Assert.True(result.Success);
        Assert.Equal(98, result.StartElevation, precision: 6);
    }

    [Fact]
    public async Task UpdatePipeResult_SerializesWithAllFields()
    {
        Container c = Create();
        CreatePipeResult created = await CreatePipeAsync(c, HdpeRequest());
        UpdatePipeResult result = await UpdatePipeAsync(c, new UpdatePipeRequest
        {
            PipeId = created.PipeId,
            LengthMeters = 12,
        });

        string json = System.Text.Json.JsonSerializer.Serialize(result, Autodesk.Mcp.Shared.Serialization.SharedJson.Options);

        Assert.Contains("\"pipeId\"", json);
        Assert.Contains("\"networkName\":\"Storm\"", json);
        Assert.Contains("\"changesApplied\"", json);
        Assert.Contains("\"length3D\"", json);
        Assert.Contains("\"success\":true", json);
        Assert.Contains("\"timestampUtc\"", json);
    }
}
