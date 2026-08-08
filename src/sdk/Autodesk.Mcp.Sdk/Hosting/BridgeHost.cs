using Autodesk.Mcp.Sdk.Communication;
using Autodesk.Mcp.Sdk.Registration;
using Microsoft.Extensions.Logging;

namespace Autodesk.Mcp.Sdk.Hosting;

/// <summary>
/// Coordinates the SDK-owned bridge lifecycle: registers the endpoint descriptor, starts the pipe
/// listener, waits for a shutdown request, then performs a graceful stop. The host application
/// (the Civil 3D plugin) is responsible for draining its own services such as the tool dispatcher.
/// </summary>
public sealed class BridgeHost : IAsyncDisposable
{
    private readonly NamedPipeServerHost _pipeHost;
    private readonly IEndpointRegistrar _registrar;
    private readonly IEndpointInfoProvider _info;
    private readonly BridgeHostOptions _options;
    private readonly BridgeShutdown _shutdown;
    private readonly ILogger<BridgeHost> _logger;
    private bool _started;

    /// <summary>Creates the host.</summary>
    public BridgeHost(
        NamedPipeServerHost pipeHost,
        IEndpointRegistrar registrar,
        IEndpointInfoProvider info,
        BridgeHostOptions options,
        BridgeShutdown shutdown,
        ILogger<BridgeHost> logger)
    {
        _pipeHost = pipeHost;
        _registrar = registrar;
        _info = info;
        _options = options;
        _shutdown = shutdown;
        _logger = logger;
    }

    /// <summary>The shutdown signal; wait on <see cref="BridgeShutdown.WaitForShutdownAsync"/>.</summary>
    public BridgeShutdown Shutdown => _shutdown;

    /// <summary>Waits until a graceful shutdown is requested.</summary>
    public Task WaitForShutdownAsync() => _shutdown.WaitForShutdownAsync();

    /// <summary>Requests a graceful shutdown (used by the <c>shutdown</c> protocol method).</summary>
    public void RequestShutdown() => _shutdown.Request();

    /// <summary>Registers the endpoint descriptor and starts the pipe listener.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
        {
            return;
        }

        await _registrar.RegisterAsync(_info.CreateEndpointDescriptor(), cancellationToken);
        await _pipeHost.StartAsync(cancellationToken);
        _started = true;

        _logger.LogInformation(
            "Bridge '{BridgeName}' ({Product} {ProductVersion}) started: pipe '{PipeName}', bridge {BridgeVersion}, protocol {ProtocolVersion}, pid {Pid}.",
            _options.BridgeName, _options.Product, _options.ProductVersion, _options.PipeName,
            _options.BridgeVersion, _options.ProtocolVersion, Environment.ProcessId);
    }

    /// <summary>Stops the pipe listener and removes the endpoint descriptor.</summary>
    public async Task StopAsync()
    {
        if (!_started)
        {
            return;
        }

        _started = false;
        await _pipeHost.StopAsync();
        await _registrar.DeleteAsync();
        _logger.LogInformation("Bridge '{BridgeName}' stopped.", _options.BridgeName);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync();
}
