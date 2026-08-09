using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Errors;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Tools.Editing.Dtos;
using Xunit;
using static Civil3D.Tools.Editing.Tests.EditingTestHarness;

namespace Civil3D.Tools.Editing.Tests;

/// <summary>
/// Material-aware SDR/PN rating validation through the create_pipe pipeline: standard ratings are
/// accepted per material, non-standard SDR/PN values are rejected with E_VALIDATION_FAILED, rigid
/// pipes (Ductile Iron) reject SDR, concrete/RCP reject both, and unknown materials skip rating
/// validation.
/// </summary>
public class CreatePipeRatingValidationTests
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

    private static CreatePipeRequest Request(string material, string? sdr, double? pressureClassBar)
        => new()
        {
            NetworkName = "Storm",
            Material = material,
            Sdr = sdr,
            PressureClassBar = pressureClassBar,
            DiameterMm = 200,
            LengthMeters = 10,
            StartEasting = 1000,
            StartNorthing = 2000,
            StartElevation = 95.5,
        };

    [Fact]
    public async Task Hdpe_NonStandardSdr_MapsToValidationFailed()
    {
        Container c = Create();

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => CreatePipeAsync(c, Request("HDPE", "99", 10)));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
        Assert.Contains("not a standard SDR for HDPE", ex.Message);
        Assert.Contains("11, 17, 26, 32.5", ex.Message);
    }

    [Fact]
    public async Task Hdpe_NonStandardPressureClass_MapsToValidationFailed()
    {
        Container c = Create();

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => CreatePipeAsync(c, Request("HDPE", "17", 100)));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
        Assert.Contains("not a standard pressure class for HDPE", ex.Message);
    }

    [Fact]
    public async Task Hdpe_UnparsableSdr_MapsToValidationFailed()
    {
        Container c = Create();

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => CreatePipeAsync(c, Request("HDPE", "seventeen", 10)));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
        Assert.Contains("not a standard SDR for HDPE", ex.Message);
    }

    [Fact]
    public async Task DuctileIron_WithSdr_MapsToValidationFailed()
    {
        Container c = Create();

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => CreatePipeAsync(c, Request("Ductile Iron", "17", 16)));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
        Assert.Contains("rated by pressure class (PN), not SDR", ex.Message);
    }

    [Fact]
    public async Task DuctileIron_Pn16WithoutSdr_Succeeds()
    {
        // A drawing whose parts list has the Ductile Iron family: the standard PN16 rating must
        // pass validation and the pipe must be created.
        var drawing = new InMemoryDrawing(
            networks:
            [
                new InMemoryDrawing.FakeNetwork(
                    100,
                    "Storm",
                    new InMemoryDrawing.FakePartFamily("Ductile Iron Pipe", 150, 200, 300)),
            ]);
        Container c = Create(drawing: drawing);

        CreatePipeResult result = await CreatePipeAsync(c, Request("Ductile Iron", null, 16));

        Assert.True(result.Success);
        Assert.Equal("Ductile Iron Pipe", result.PartFamilyName);
    }

    [Fact]
    public async Task Concrete_WithSdr_MapsToValidationFailed()
    {
        Container c = Create();

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => CreatePipeAsync(c, Request("RCP", "35", null)));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
        Assert.Contains("have no SDR/PN rating", ex.Message);
    }

    [Fact]
    public async Task Concrete_WithPressureClass_MapsToValidationFailed()
    {
        Container c = Create();

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => CreatePipeAsync(c, Request("Concrete", null, 10)));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
        Assert.Contains("have no PN pressure class", ex.Message);
    }

    [Fact]
    public async Task Pvc_StandardSdr35Pn10_Succeeds()
    {
        // The default drawing has a "PVC Pipe" family; SDR35 / PN10 are standard for PVC so
        // validation passes and the pipe is created via the bare-material fallback.
        Container c = Create();

        CreatePipeResult result = await CreatePipeAsync(c, Request("PVC", "35", 10));

        Assert.True(result.Success);
        Assert.Equal("PVC Pipe", result.PartFamilyName);
    }

    [Fact]
    public async Task UnknownMaterial_SkipsRatingValidation()
    {
        Container c = Create();

        // Unknown materials keep the existing text-match behaviour: the rating is not validated
        // and the failure (if any) is the missing family, not the rating.
        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => CreatePipeAsync(c, Request("Polypropylene", "17", 10)));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
        Assert.Contains("No pipe part family", ex.Message);
        Assert.DoesNotContain("not a standard SDR", ex.Message);
    }

    [Fact]
    public async Task Rcp_AliasResolvesToConcreteFamily()
    {
        // A drawing whose parts list names the family "Concrete Pipe": the "RCP" alias must
        // resolve to the canonical "Concrete" name so the pipe can be created.
        var drawing = new InMemoryDrawing(
            networks:
            [
                new InMemoryDrawing.FakeNetwork(
                    100,
                    "Storm",
                    new InMemoryDrawing.FakePartFamily("Concrete Pipe", 150, 200, 300)),
            ]);
        Container c = Create(drawing: drawing);

        CreatePipeResult result = await CreatePipeAsync(c, Request("RCP", null, null));

        Assert.True(result.Success);
        Assert.Equal("Concrete Pipe", result.PartFamilyName);
    }

    [Fact]
    public async Task Di_AliasResolvesToDuctileIronFamily()
    {
        var drawing = new InMemoryDrawing(
            networks:
            [
                new InMemoryDrawing.FakeNetwork(
                    100,
                    "Storm",
                    new InMemoryDrawing.FakePartFamily("Ductile Iron Pipe", 150, 200, 300)),
            ]);
        Container c = Create(drawing: drawing);

        CreatePipeResult result = await CreatePipeAsync(c, Request("DI", null, 16));

        Assert.True(result.Success);
        Assert.Equal("Ductile Iron Pipe", result.PartFamilyName);
    }
}
