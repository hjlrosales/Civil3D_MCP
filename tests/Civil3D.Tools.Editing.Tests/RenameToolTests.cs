using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Errors;
using Civil3D.Domain.Commands;
using Civil3D.Tools.Editing.Dtos;
using Xunit;
using static Civil3D.Tools.Editing.Tests.EditingTestHarness;

namespace Civil3D.Tools.Editing.Tests;

/// <summary>
/// The rename tools through the full command pipeline (validation → confirmation → write
/// transaction → commit/rollback → domain events → protocol response): success, error mapping,
/// events, undo registration and serialization.
/// </summary>
public class RenameToolTests
{
    private static async Task<RenameResult> RenameAlignmentAsync(Container c, long id, string newName)
    {
        var context = new ToolExecutionContext
        {
            ToolName = "rename_alignment",
            CorrelationId = "c-1",
            SessionId = "s-1",
        };
        var parameters = System.Text.Json.JsonSerializer.SerializeToElement(
            new RenameAlignmentRequest { ObjectId = id, NewName = newName },
            Autodesk.Mcp.Shared.Serialization.SharedJson.Options);
        return (RenameResult)(await c.AlignmentTool.ExecuteAsync(context, parameters))!;
    }

    private static async Task<RenameResult> RenameSurfaceAsync(Container c, long id, string newName)
    {
        var context = new ToolExecutionContext
        {
            ToolName = "rename_surface",
            CorrelationId = "c-1",
            SessionId = "s-1",
        };
        var parameters = System.Text.Json.JsonSerializer.SerializeToElement(
            new RenameSurfaceRequest { ObjectId = id, NewName = newName },
            Autodesk.Mcp.Shared.Serialization.SharedJson.Options);
        return (RenameResult)(await c.SurfaceTool.ExecuteAsync(context, parameters))!;
    }

    [Fact]
    public async Task RenameAlignment_Succeeds_CommitsAndRaisesEvents()
    {
        Container c = Create();

        RenameResult result = await RenameAlignmentAsync(c, 1, "Mainline Renamed");

        Assert.True(result.Success);
        Assert.Equal(1, result.ObjectId);
        Assert.Equal("Mainline", result.PreviousName);
        Assert.Equal("Mainline Renamed", result.CurrentName);
        Assert.Equal("Mainline Renamed", c.Drawing.FindAlignment(1)!.Name);
        Assert.Contains(c.Events.Published, e => e is ObjectRenamed { ObjectType: "alignment", ObjectId: 1, PreviousName: "Mainline", NewName: "Mainline Renamed" });
        Assert.Contains(c.Events.Published, e => e is TransactionCommitted);
        Assert.Contains(c.Events.Published, e => e is CommandCompleted);
        Assert.Single(c.Undo.Units);
    }

    [Fact]
    public async Task RenameSurface_Succeeds_CommitsAndRaisesEvents()
    {
        Container c = Create();

        RenameResult result = await RenameSurfaceAsync(c, 10, "EG Final");

        Assert.True(result.Success);
        Assert.Equal(10, result.ObjectId);
        Assert.Equal("EG", result.PreviousName);
        Assert.Equal("EG Final", result.CurrentName);
        Assert.Equal("EG Final", c.Drawing.FindSurface(10)!.Name);
        Assert.Contains(c.Events.Published, e => e is ObjectRenamed { ObjectType: "surface", ObjectId: 10 });
        Assert.Single(c.Undo.Units);
    }

    [Fact]
    public async Task RenameAlignment_MissingObject_ThrowsObjectNotFound()
    {
        Container c = Create();

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(() => RenameAlignmentAsync(c, 999, "Nope"));

        Assert.Equal(ErrorCode.E_OBJECT_NOT_FOUND, ex.ErrorCode);
        Assert.Empty(c.Events.Published);
    }

    [Fact]
    public async Task RenameAlignment_DuplicateName_MapsToValidationFailed()
    {
        Container c = Create();

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(() => RenameAlignmentAsync(c, 1, "Ramp A"));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
        Assert.Contains("already exists", ex.Message);
        Assert.Equal("Mainline", c.Drawing.FindAlignment(1)!.Name); // rolled back
    }

    [Fact]
    public async Task RenameAlignment_EmptyName_MapsToValidationFailed()
    {
        Container c = Create();

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(() => RenameAlignmentAsync(c, 1, "   "));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
        Assert.Contains("must not be empty", ex.Message);
        Assert.Equal("Mainline", c.Drawing.FindAlignment(1)!.Name);
    }

    [Fact]
    public async Task RenameAlignment_NoOp_MapsToValidationFailed()
    {
        Container c = Create();

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(() => RenameAlignmentAsync(c, 1, "mainline"));

        Assert.Equal(ErrorCode.E_VALIDATION_FAILED, ex.ErrorCode);
        Assert.Contains("already named", ex.Message);
    }

    [Fact]
    public async Task RenameAlignment_ConfirmationRequired_AndDenied()
    {
        Container c = Create(requireConfirmation: true); // NullConfirmationGate denies

        BridgeException ex = await Assert.ThrowsAsync<BridgeException>(() => RenameAlignmentAsync(c, 1, "New Name"));

        Assert.Equal(ErrorCode.E_CONFIRMATION_REQUIRED, ex.ErrorCode);
        Assert.Equal("Mainline", c.Drawing.FindAlignment(1)!.Name);
    }

    [Fact]
    public async Task RenameAlignment_ConfirmationRequired_AndGranted()
    {
        Container c = Create(requireConfirmation: true, confirmationGate: new GrantingConfirmationGate());

        RenameResult result = await RenameAlignmentAsync(c, 1, "Confirmed Name");

        Assert.True(result.Success);
        Assert.Equal("Confirmed Name", c.Drawing.FindAlignment(1)!.Name);
    }

    [Fact]
    public async Task RenameResult_SerializesWithAllFields()
    {
        Container c = Create();
        RenameResult result = await RenameAlignmentAsync(c, 2, "Ramp B");

        string json = System.Text.Json.JsonSerializer.Serialize(result, Autodesk.Mcp.Shared.Serialization.SharedJson.Options);

        Assert.Contains("\"objectId\":2", json);
        Assert.Contains("\"previousName\":\"Ramp A\"", json);
        Assert.Contains("\"currentName\":\"Ramp B\"", json);
        Assert.Contains("\"success\":true", json);
        Assert.Contains("\"timestampUtc\"", json);
    }
}
