using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Autodesk.Mcp.Shared.Errors;
using Civil3D.Domain.Profiles.Dtos;
using Civil3D.Domain.Profiles.Services;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Query.Dtos;

namespace Civil3D.Tools.Query.Tools;

/// <summary>
/// Tool <c>get_profile</c>: returns a single profile by its stable numeric id. Read-only.
/// Fails with E_NO_ACTIVE_DOCUMENT when no drawing is open and E_OBJECT_NOT_FOUND when the id
/// does not exist.
/// </summary>
[McpTool(
    "get_profile",
    "Get Profile",
    "Returns a single profile by id. Read-only; fails with E_NO_ACTIVE_DOCUMENT when no drawing " +
    "is open and E_OBJECT_NOT_FOUND when the id does not exist.",
    Category = ToolCategory.Profiles,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "profiles", "lookup", "read-only" })]
public sealed class GetProfileTool : QueryToolBase<IdRequest, ProfileInfo>
{
    private readonly IProfileService _profiles;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="profiles">The profile domain service.</param>
    public GetProfileTool(ICivil3DSession session, IProfileService profiles)
        : base(session)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    }

    /// <inheritdoc />
    protected override Task<ProfileInfo> ExecuteToolCoreAsync(
        IdRequest input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireActiveDrawing(context);

        ProfileInfo? result = RunQuery(context, () => _profiles.GetById(input.Id));
        return Task.FromResult(result ?? throw new BridgeException(
            ErrorCode.E_OBJECT_NOT_FOUND,
            $"No profile with id {input.Id} was found.",
            context.CorrelationId,
            context.SessionId));
    }
}
