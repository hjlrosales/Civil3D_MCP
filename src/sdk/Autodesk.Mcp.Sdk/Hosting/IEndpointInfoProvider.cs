using Autodesk.Mcp.Shared.Dtos;

namespace Autodesk.Mcp.Sdk.Hosting;

/// <summary>
/// Supplies the bridge identity and capabilities, consumed by the handshake, the endpoint
/// registrar and the <c>bridge/version</c> / <c>bridge/getCapabilities</c> health tools.
/// </summary>
public interface IEndpointInfoProvider
{
    /// <summary>Returns the runtime bridge information (handshake payload).</summary>
    BridgeInformation GetBridgeInformation();

    /// <summary>Creates the discovery descriptor written to the endpoint registry.</summary>
    EndpointDescriptor CreateEndpointDescriptor();
}
