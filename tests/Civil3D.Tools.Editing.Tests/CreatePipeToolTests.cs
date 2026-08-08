using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Errors;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Tools.Editing.Dtos;
using Xunit;
using static Civil3D.Tools.Editing.Tests.EditingTestHarness;

namespace Civil3D.Tools.Editing.Tests;

/// <summary>
/// The <c>create_pipe</c> tool through the full command pipeline (validation → confirmation →
/// write transaction → commit/rollback → domain events → protocol response): success, part
/// family resolution (default match text, explicit override, ambiguous, not found), network not
/// found, structural validation failures, confirmation gating and undo registration.
/// </summary>
public class CreatePipeToolTests
{
    private static async Task<CreatePipeResult> CreatePipeAsync(Container c, CreatePipeRequest request)
    {
        var context = new ToolExecutionContext
        {
            ToolName = "create_pipe",
            CorrelationId = "c-1",
            SessionId = "s-1",
        };
        var parameters = System.Text.Json.JsonSerializer.SerializeToElement(
            request, Autodesk.Mcp.Shared.Serialization.SharedJson.Options);
        return (CreatePipeResult)(await c.CreatePipeTool.ExecuteAsync(context, parameters))!;
    }

    private static CreatePipeRequest HdpeRequest(string networkName = "Storm") => new()
    {
        NetworkName = networkName,
        Material = "HDPE",
        Sdr = "17",
        PressureClassBar = 10,
        DiameterMm = 200,
        LengthMeters = 10,
        StartEasting = 1000,
        StartNorthing = 2000,
        StartElevation = 95.5,
    };

    [Fact]
    public async Task CreatePipe_Succeeds_CommitsAndRaisesEvents()
    {
        Container c = Create();

        CreatePipeResult result = await CreatePipeAsync(c, HdpeRequest());

        Assert.True(result.Success);
        Assert.Equal("Storm", result.NetworkName);
        Assert.Equal("HDPE SDR17 PN10 Pipe", result.PartFamilyName);
        Assert.Equal(0.2, result.InnerDiameterOrWidth, precision: 6); // 200 mm exact match, in meters
        Assert.Equal(1000, result.StartEasting);
        Assert.Equal(2000, result.StartNorthing);
        Assert.Equal(1010, result.EndEasting); // direction 0 => +Easting axis
        Assert.Equal(2000, result.EndNorthing);
        Assert.Equal(95.5, result.EndElevation); // horizontal: same as start
        Assert.Equal(10, result.Length3D, precision: 6);
        Assert.Single(c.Drawing.Networks.Single(n => n.Name == "Storm").Pipes);
        Assert.Contains(c.Events.Published, e => e is Civil3D.Domain.Commands.PartCreated { PartType: "pipe", NetworkId: 100 });
        Assert.Contains(c.Events.Published, e => e is Civil3D.Domain.Commands.TransactionCommitted);
        Assert.Single(c.Undo.Units);
    }

    [Fact]
    public async Task CreatePipe_DirectionAndLength_ComputeExpectedEndpoint()
    {
        Container c = Create();
        CreatePipeRequest request = HdpeRequest() with { DirectionDegrees = 90 }; // +Northing axis

        CreatePipeResult result = await CreatePipeAsync(c, request);

        Assert.Equal(1000, result.EndEasting, precision: 6);
        Assert.Equal(2010, result.EndNorthing, precision: 6);
    }

    [Fact]
    public async Task CreatePipe_ExplicitPartFamilyMatch_Overrides_MaterialFields()
    {
        Container c = Create();
        CreatePipeRequest request = HdpeRequest() with { PartFamilyMatch = "PVC" };

        CreatePipeResult result = await CreatePipeAsync(c, request);

        Assert.Equal("PVC Pipe", result.PartFamilyName);
    }

    [Fact]
    public async Task CreatePipe_DiameterSnapsToClosestAvailableSize()
    {
        Container c = Create();
        CreatePipeRequest request = HdpeRequest() with { DiameterMm = 180 }; // between 150 and 200

        CreatePipeResult result = await CreatePipeAsync(c, request);

        Assert.Equal(0.2, result.InnerDiameterOrWidth, precision: 6); // 200 mm is closer than 150 mm
    }

    [Fact]
    public async Task CreatePipe_NetworkNotFound_ThrowsObjectNotFound()
    {
        Container c = Create();

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => CreatePipeAsync(c, HdpeRequest(networkName: "Nonexistent")));

        Assert.Equal(ErrorCode.E_OBJECT_NOT_FOUND, ex.ErrorCode);
        Assert.Empty(c.Drawing.Networks.Single(n => n.Name == "Storm").Pipes);
    }

    [Fact]
    public async Task CreatePipe_NoMatchingPartFamily_MapsToValidationFailed()
    {
        Container c = Create();
        CreatePipeRequest request = HdpeRequest() with { Material = "Concrete" };

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(() => CreatePipeAsync(c, request));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
        Assert.Contains("No pipe part family", ex.Message);
        Assert.Empty(c.Drawing.Networks.Single(n => n.Name == "Storm").Pipes);
    }

    [Fact]
    public async Task CreatePipe_AmbiguousPartFamilyMatch_MapsToValidationFailed()
    {
        Container c = Create();
        // Both fake families' descriptions contain "Pipe".
        CreatePipeRequest request = HdpeRequest() with { PartFamilyMatch = "Pipe" };

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(() => CreatePipeAsync(c, request));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
        Assert.Contains("more than one", ex.Message);
    }

    [Fact]
    public async Task CreatePipe_ZeroDiameter_MapsToValidationFailed()
    {
        Container c = Create();
        CreatePipeRequest request = HdpeRequest() with { DiameterMm = 0 };

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(() => CreatePipeAsync(c, request));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
    }

    [Fact]
    public async Task CreatePipe_ZeroLength_MapsToValidationFailed()
    {
        Container c = Create();
        CreatePipeRequest request = HdpeRequest() with { LengthMeters = 0 };

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(() => CreatePipeAsync(c, request));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
    }

    [Fact]
    public async Task CreatePipe_EmptyNetworkName_MapsToValidationFailed()
    {
        Container c = Create();
        CreatePipeRequest request = HdpeRequest() with { NetworkName = "   " };

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(() => CreatePipeAsync(c, request));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
    }

    [Fact]
    public async Task CreatePipe_ConfirmationRequired_AndDenied()
    {
        Container c = Create(requireConfirmation: true); // NullConfirmationGate denies

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(() => CreatePipeAsync(c, HdpeRequest()));

        Assert.Equal(ErrorCode.E_CONFIRMATION_REQUIRED, ex.ErrorCode);
        Assert.Empty(c.Drawing.Networks.Single(n => n.Name == "Storm").Pipes);
    }

    [Fact]
    public async Task CreatePipe_ConfirmationRequired_AndGranted()
    {
        Container c = Create(requireConfirmation: true, confirmationGate: new GrantingConfirmationGate());

        CreatePipeResult result = await CreatePipeAsync(c, HdpeRequest());

        Assert.True(result.Success);
        Assert.Single(c.Drawing.Networks.Single(n => n.Name == "Storm").Pipes);
    }

    [Fact]
    public async Task CreatePipeResult_SerializesWithAllFields()
    {
        Container c = Create();
        CreatePipeResult result = await CreatePipeAsync(c, HdpeRequest());

        string json = System.Text.Json.JsonSerializer.Serialize(result, Autodesk.Mcp.Shared.Serialization.SharedJson.Options);

        Assert.Contains("\"networkName\":\"Storm\"", json);
        Assert.Contains("\"partFamilyName\":\"HDPE SDR17 PN10 Pipe\"", json);
        Assert.Contains("\"success\":true", json);
        Assert.Contains("\"timestampUtc\"", json);
    }
}
