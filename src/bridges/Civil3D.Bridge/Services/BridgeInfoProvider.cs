using Autodesk.Mcp.Sdk.Hosting;
using Autodesk.Mcp.Shared.Dtos;
using Civil3D.Bridge.Configuration;

namespace Civil3D.Bridge.Services;

/// <summary>
/// Supplies the bridge identity and capabilities from <see cref="BridgeOptions"/>; implements
/// <see cref="IEndpointInfoProvider"/> for the handshake, the endpoint registrar and the health tools.
/// </summary>
public sealed class BridgeInfoProvider : IEndpointInfoProvider
{
    private readonly BridgeOptions _options;
    private readonly DateTimeOffset _startedAtUtc = DateTimeOffset.UtcNow;

    /// <summary>Creates the provider.</summary>
    /// <param name="options">The raw bridge configuration.</param>
    public BridgeInfoProvider(BridgeOptions options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public BridgeInformation GetBridgeInformation() => new()
    {
        BridgeName = _options.BridgeName,
        Product = _options.Product,
        ProductVersion = _options.ProductVersion,
        BridgeVersion = VersionOr(_options.BridgeVersion, new(1, 0, 0)),
        SdkVersion = VersionOr(_options.SdkVersion, new(1, 0, 0)),
        ProtocolVersion = Autodesk.Mcp.Shared.Contracts.ProtocolConstants.CurrentProtocolVersion,
        Capabilities = BuildCapabilities(),
    };

    /// <inheritdoc />
    public EndpointDescriptor CreateEndpointDescriptor() => new()
    {
        BridgeName = _options.BridgeName,
        Product = _options.Product,
        ProductVersion = _options.ProductVersion,
        BridgeVersion = VersionOr(_options.BridgeVersion, new(1, 0, 0)),
        SdkVersion = VersionOr(_options.SdkVersion, new(1, 0, 0)),
        ProtocolVersion = Autodesk.Mcp.Shared.Contracts.ProtocolConstants.CurrentProtocolVersion,
        PipeName = _options.PipeName,
        ProcessId = Environment.ProcessId,
        StartedAtUtc = _startedAtUtc,
        Capabilities = BuildCapabilities(),
    };

    private BridgeCapabilities BuildCapabilities() => new()
    {
        SupportsStreaming = _options.SupportsStreaming,
        SupportsProgress = _options.SupportsProgress,
        SupportsCancellation = _options.SupportsCancellation,
        SupportsConfirmation = _options.SupportsConfirmation,
        SupportsBatchRequests = _options.SupportsBatchRequests,
        SupportsParallelExecution = _options.SupportsParallelExecution,
        SupportedProtocolVersion = Autodesk.Mcp.Shared.Contracts.ProtocolConstants.CurrentProtocolVersion,
        SupportedProducts = _options.SupportedProducts,
    };

    private static Autodesk.Mcp.Shared.Contracts.VersionInformation VersionOr(string value, Autodesk.Mcp.Shared.Contracts.VersionInformation fallback)
        => Autodesk.Mcp.Shared.Contracts.VersionInformation.TryParse(value, out var version) ? version : fallback;
}
