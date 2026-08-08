using Autodesk.Mcp.Shared.Dtos;
using Autodesk.Mcp.Shared.Enums;
using Autodesk.Mcp.Sdk.Hosting;

namespace Autodesk.Mcp.Sdk.Tools.Health;

/// <summary>
/// Health tool <c>bridge/version</c>: returns the bridge identity and version information
/// (bridge, SDK and protocol versions, product, pipe name, capabilities).
/// </summary>
[McpTool(
    "bridge/version",
    "Bridge Version",
    "Returns the bridge identity and version information: bridge version, SDK version, protocol " +
    "version, product, pipe name and advertised capabilities.",
    Category = ToolCategory.General,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low)]
public sealed class BridgeVersionTool : ToolBase<EmptyParameters, BridgeInformation>
{
    private readonly IEndpointInfoProvider _info;

    /// <summary>Creates the tool.</summary>
    /// <param name="info">Bridge information provider.</param>
    public BridgeVersionTool(IEndpointInfoProvider info)
    {
        _info = info;
    }

    /// <inheritdoc />
    protected override Task<BridgeInformation> ExecuteCoreAsync(EmptyParameters input, ToolExecutionContext context, CancellationToken cancellationToken)
        => Task.FromResult(_info.GetBridgeInformation());
}
