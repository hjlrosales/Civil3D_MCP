using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Dtos;
using Autodesk.Mcp.Shared.Enums;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Hosting;

namespace Autodesk.Mcp.Sdk.Tools.Health;

/// <summary>
/// Health tool <c>bridge/getCapabilities</c>: returns bridge information, the protocol version,
/// the advertised capabilities and the complete tool manifest list served by this bridge.
/// </summary>
[McpTool(
    "bridge/getCapabilities",
    "Bridge Capabilities",
    "Returns the bridge information, protocol version, capabilities and the full tool manifest " +
    "list served by this bridge.",
    Category = ToolCategory.General,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low)]
public sealed class BridgeGetCapabilitiesTool : ToolBase<EmptyParameters, GetCapabilitiesResult>
{
    private readonly IEndpointInfoProvider _info;
    private readonly IToolCatalog _catalog;

    /// <summary>Creates the tool.</summary>
    /// <param name="info">Bridge information provider.</param>
    /// <param name="catalog">Tool catalog whose manifests are reported.</param>
    public BridgeGetCapabilitiesTool(IEndpointInfoProvider info, IToolCatalog catalog)
    {
        _info = info;
        _catalog = catalog;
    }

    /// <inheritdoc />
    protected override Task<GetCapabilitiesResult> ExecuteCoreAsync(EmptyParameters input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        BridgeInformation bridge = _info.GetBridgeInformation();
        return Task.FromResult(new GetCapabilitiesResult
        {
            Bridge = bridge,
            ProtocolVersion = ProtocolConstants.CurrentProtocolVersion,
            Capabilities = bridge.Capabilities ?? new BridgeCapabilities(),
            Tools = _catalog.Manifests,
        });
    }
}
