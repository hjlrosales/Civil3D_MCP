using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Errors;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Tools.Editing.Dtos;
using Xunit;
using static Civil3D.Tools.Editing.Tests.EditingTestHarness;

namespace Civil3D.Tools.Editing.Tests;

/// <summary>
/// The <c>create_pipe_network</c> tool through the full command pipeline: success (network +
/// parts list + material families), duplicate-name rejection, structural validation, default
/// materials, confirmation gating and undo registration.
/// </summary>
public class CreatePipeNetworkToolTests
{
    private static async Task<CreatePipeNetworkResult> CreateNetworkAsync(Container c, CreatePipeNetworkRequest request)
    {
        var context = new ToolExecutionContext
        {
            ToolName = "create_pipe_network",
            CorrelationId = "c-1",
            SessionId = "s-1",
        };
        var parameters = System.Text.Json.JsonSerializer.SerializeToElement(
            request, Autodesk.Mcp.Shared.Serialization.SharedJson.Options);
        return (CreatePipeNetworkResult)(await c.CreatePipeNetworkTool.ExecuteAsync(context, parameters))!;
    }

    [Fact]
    public async Task CreateNetwork_Succeeds_CommitsAndRaisesEvents()
    {
        Container c = Create();

        CreatePipeNetworkResult result = await CreateNetworkAsync(c, new CreatePipeNetworkRequest
        {
            Name = "Sanitary",
            Description = "Test network",
            Materials = ["HDPE", "PVC"],
        });

        Assert.True(result.Success);
        Assert.Equal("Sanitary", result.Name);
        Assert.Equal("Test network", result.Description);
        Assert.Equal("Sanitary Parts List", result.PartsListName);
        Assert.Equal(new[] { "HDPE Pipe", "PVC Pipe" }, result.FamiliesAdded);
        Assert.Empty(result.FamiliesFailed);
        Assert.Contains(c.Drawing.Networks, n => n.Name == "Sanitary");
        Assert.Contains(c.Events.Published, e => e is Civil3D.Domain.Commands.NetworkCreated { NetworkName: "Sanitary" });
        Assert.Contains(c.Events.Published, e => e is Civil3D.Domain.Commands.TransactionCommitted);
        Assert.Single(c.Undo.Units);
    }

    [Fact]
    public async Task CreateNetwork_DefaultMaterials_IncludeHdpe()
    {
        Container c = Create();

        CreatePipeNetworkResult result = await CreateNetworkAsync(c, new CreatePipeNetworkRequest { Name = "Main" });

        Assert.True(result.Success);
        Assert.Contains("HDPE Pipe", result.FamiliesAdded);
    }

    [Fact]
    public async Task CreateNetwork_OmittedSizes_DefaultToCommonGravityPipeRange()
    {
        Container c = Create();

        CreatePipeNetworkResult result = await CreateNetworkAsync(c, new CreatePipeNetworkRequest
        {
            Name = "Main",
            Materials = ["HDPE"],
        });

        Assert.True(result.Success);
        InMemoryDrawing.FakeNetwork network = Assert.Single(c.Drawing.Networks, n => n.Name == "Main");
        InMemoryDrawing.FakePartFamily hdpe = Assert.Single(network.PartFamilies);
        Assert.Equal(new[] { 100.0, 150, 200, 250, 300 }, hdpe.Sizes.Select(s => s.DiameterMm));
    }

    [Fact]
    public async Task CreateNetwork_SizesMm_AddRequestedSizesToFamilies()
    {
        Container c = Create();

        CreatePipeNetworkResult result = await CreateNetworkAsync(c, new CreatePipeNetworkRequest
        {
            Name = "Sanitary",
            Materials = ["HDPE"],
            SizesMm = [200],
        });

        Assert.True(result.Success);
        InMemoryDrawing.FakeNetwork network = Assert.Single(c.Drawing.Networks, n => n.Name == "Sanitary");
        InMemoryDrawing.FakePartFamily hdpe = Assert.Single(network.PartFamilies);
        Assert.Equal(new[] { 200.0 }, hdpe.Sizes.Select(s => s.DiameterMm));
    }

    [Fact]
    public async Task CreateNetwork_InvalidSize_MapsToValidationFailed()
    {
        Container c = Create();

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => CreateNetworkAsync(c, new CreatePipeNetworkRequest { Name = "Main", SizesMm = [0] }));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
    }

    [Fact]
    public async Task CreateNetwork_ExistingName_MapsToValidationFailed()
    {
        Container c = Create(); // default drawing already has network "Storm"

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => CreateNetworkAsync(c, new CreatePipeNetworkRequest { Name = "Storm" }));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
        Assert.Contains("already exists", ex.Message);
        Assert.Single(c.Drawing.Networks); // unchanged
    }

    [Fact]
    public async Task CreateNetwork_EmptyName_MapsToValidationFailed()
    {
        Container c = Create();

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => CreateNetworkAsync(c, new CreatePipeNetworkRequest { Name = "   " }));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
    }

    [Fact]
    public async Task CreateNetwork_BlankMaterial_MapsToValidationFailed()
    {
        Container c = Create();

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => CreateNetworkAsync(c, new CreatePipeNetworkRequest { Name = "Main", Materials = [" "] }));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
    }

    [Fact]
    public async Task CreateNetwork_ConfirmationRequired_AndDenied()
    {
        Container c = Create(requireConfirmation: true); // NullConfirmationGate denies

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(
            () => CreateNetworkAsync(c, new CreatePipeNetworkRequest { Name = "Sanitary" }));

        Assert.Equal(ErrorCode.E_CONFIRMATION_REQUIRED, ex.ErrorCode);
        Assert.DoesNotContain(c.Drawing.Networks, n => n.Name == "Sanitary");
    }

    [Fact]
    public async Task CreateNetwork_ConfirmationRequired_AndGranted()
    {
        Container c = Create(requireConfirmation: true, confirmationGate: new GrantingConfirmationGate());

        CreatePipeNetworkResult result = await CreateNetworkAsync(c, new CreatePipeNetworkRequest { Name = "Sanitary" });

        Assert.True(result.Success);
        Assert.Contains(c.Drawing.Networks, n => n.Name == "Sanitary");
    }

    [Fact]
    public async Task CreateNetworkResult_SerializesWithAllFields()
    {
        Container c = Create();
        CreatePipeNetworkResult result = await CreateNetworkAsync(c, new CreatePipeNetworkRequest { Name = "Main" });

        string json = System.Text.Json.JsonSerializer.Serialize(result, Autodesk.Mcp.Shared.Serialization.SharedJson.Options);

        Assert.Contains("\"networkId\"", json);
        Assert.Contains("\"name\":\"Main\"", json);
        Assert.Contains("\"partsListName\":\"Main Parts List\"", json);
        Assert.Contains("\"familiesAdded\"", json);
        Assert.Contains("\"success\":true", json);
        Assert.Contains("\"timestampUtc\"", json);
    }
}
