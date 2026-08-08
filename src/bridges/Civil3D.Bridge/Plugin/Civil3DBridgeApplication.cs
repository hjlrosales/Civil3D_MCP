using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Mcp.Sdk.Hosting;
using Civil3D.Bridge.Configuration;
using Civil3D.Bridge.DependencyInjection;
using Civil3D.Bridge.Execution;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using SysException = System.Exception;

namespace Civil3D.Bridge.Plugin;

/// <summary>
/// Civil 3D plugin entry point (<c>IExtensionApplication</c>, loaded via NETLOAD or auto-load).
/// Builds the DI container, starts the tool dispatcher and the bridge host (pipe listener +
/// endpoint registration). On termination it requests a graceful shutdown, stops the dispatcher
/// and disposes the container.
/// </summary>
public sealed class Civil3DBridgeApplication : IExtensionApplication
{
    private ServiceProvider? _provider;
    private BridgeHost? _host;
    private ToolDispatcher? _dispatcher;
    private ILogger? _logger;
    private BridgeOptions? _options;

    /// <summary>Called by Civil 3D after the assembly is loaded (application context).</summary>
    public void Initialize()
    {
        try
        {
            _options = LoadConfiguration();
            Log.Logger = CreateSerilogLogger(_options);

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddSerilog(Log.Logger, dispose: false));
            services.AddCivil3DBridge(_options);

            _provider = services.BuildServiceProvider();
            _logger = _provider.GetRequiredService<ILogger<Civil3DBridgeApplication>>();

            _dispatcher = _provider.GetRequiredService<ToolDispatcher>();
            _dispatcher.Start();

            _host = _provider.GetRequiredService<BridgeHost>();
            _host.StartAsync().GetAwaiter().GetResult();

            _logger.LogInformation(
                "Civil 3D Bridge initialized: {BridgeName} (pipe {PipeName}, pid {Pid}, protocol {Protocol}).",
                _options.BridgeName, _options.PipeName, Environment.ProcessId,
                Autodesk.Mcp.Shared.Contracts.ProtocolConstants.CurrentProtocolVersion);
        }
        catch (SysException ex)
        {
            Log.Error(ex, "Civil 3D Bridge failed to initialize.");
            try
            {
                Application.ShowAlertDialog($"Civil 3D Bridge failed to initialize. See the bridge log for details.\n\n{ex.Message}");
            }
            catch
            {
                // Best effort; the alert is informational only.
            }
        }
    }

    /// <summary>Called by Civil 3D during shutdown (application context).</summary>
    public void Terminate()
    {
        try
        {
            _host?.RequestShutdown();
            _host?.StopAsync().GetAwaiter().GetResult();
            _dispatcher?.StopAsync().GetAwaiter().GetResult();
            _logger?.LogInformation("Civil 3D Bridge terminated.");
        }
        catch (SysException ex)
        {
            Log.Error(ex, "Civil 3D Bridge failed during termination.");
        }
        finally
        {
            _provider?.Dispose();
            Log.CloseAndFlush();
        }
    }

    private static BridgeOptions LoadConfiguration()
    {
        // Under NETLOAD, AppContext.BaseDirectory is the application directory (for example the
        // acad.exe folder), not the plugin folder — so resolve relative to this assembly instead.
        string baseDirectory = Path.GetDirectoryName(typeof(Civil3DBridgeApplication).Assembly.Location)
            ?? AppContext.BaseDirectory;
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(baseDirectory)
            .AddJsonFile("Configuration/bridge.config.json", optional: true, reloadOnChange: false)
            .Build();

        BridgeOptions options = new();
        configuration.GetSection("bridge").Bind(options);
        if (string.IsNullOrWhiteSpace(options.LogDirectory))
        {
            options.LogDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AutodeskMcp",
                "logs");
        }

        if (string.IsNullOrWhiteSpace(options.PipeName))
        {
            options.PipeName = $"{Autodesk.Mcp.Shared.Contracts.ProtocolConstants.PipeNamePrefix}{options.Product.ToLowerInvariant()}-{Environment.ProcessId}";
        }

        return options;
    }

    private static Serilog.Core.Logger CreateSerilogLogger(BridgeOptions options)
    {
        Directory.CreateDirectory(options.LogDirectory);
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.WithProperty("Bridge", options.BridgeName)
            .Enrich.WithProperty("Pid", Environment.ProcessId)
            .WriteTo.File(
                Path.Combine(options.LogDirectory, "civil3d-bridge-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({Bridge}/{Pid}) {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
