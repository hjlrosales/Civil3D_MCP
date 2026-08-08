using Autodesk.Mcp.Sdk.Hosting;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Dtos;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Drawing.Dtos;

namespace Civil3D.Tools.Drawing.Tools;

/// <summary>
/// Tool <c>drawing_info</c>: returns identity, state and version metadata of the active drawing
/// (name, path, DWG version, modification/read-only state, current layout, model-space status,
/// database fingerprint, Civil 3D version, bridge/protocol/SDK versions and open-document counts).
/// Read-only. Requires an active document; otherwise returns <c>E_NO_ACTIVE_DOCUMENT</c>.
/// </summary>
[McpTool(
    "drawing_info",
    "Drawing Info",
    "Returns information about the active drawing: name, path, DWG file version, modification and " +
    "read-only state, current layout, model-space status, database fingerprint, Civil 3D version and " +
    "the bridge, protocol and SDK versions. Read-only. Fails with E_NO_ACTIVE_DOCUMENT when no " +
    "drawing is open.",
    Category = ToolCategory.Drawing,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "drawing", "info", "read-only" })]
public sealed class DrawingInfoTool : Civil3DToolBase<EmptyParameters, DrawingInfoDto>
{
    private readonly IEndpointInfoProvider _bridgeInfo;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="bridgeInfo">Bridge identity provider (bridge, protocol and SDK versions).</param>
    public DrawingInfoTool(ICivil3DSession session, IEndpointInfoProvider bridgeInfo)
        : base(session)
    {
        _bridgeInfo = bridgeInfo ?? throw new ArgumentNullException(nameof(bridgeInfo));
    }

    /// <inheritdoc />
    protected override Task<DrawingInfoDto> ExecuteToolCoreAsync(EmptyParameters input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ActiveDrawing drawing = RequireActiveDrawing(context);
        BridgeInformation bridge = _bridgeInfo.GetBridgeInformation();

        return Task.FromResult(new DrawingInfoDto
        {
            DrawingName = drawing.DrawingName,
            DrawingPath = drawing.DrawingPath,
            DrawingVersion = drawing.DrawingVersion,
            IsModified = drawing.IsModified,
            IsReadOnly = drawing.IsReadOnly,
            CurrentLayout = drawing.CurrentLayout,
            IsModelSpaceActive = drawing.IsModelSpaceActive,
            DatabaseFingerprint = drawing.DatabaseFingerprint,
            Civil3DVersion = drawing.Civil3DVersion,
            BridgeVersion = bridge.BridgeVersion.ToString(),
            ProtocolVersion = bridge.ProtocolVersion.ToString(),
            SdkVersion = bridge.SdkVersion.ToString(),
            OpenDocumentsCount = drawing.OpenDocumentsCount,
            CurrentDocumentName = drawing.CurrentDocumentName,
            CurrentDocumentPath = drawing.CurrentDocumentPath,
        });
    }
}
