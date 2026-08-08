using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Mcp.Sdk.Hosting;
using Civil3D.Bridge.Configuration;
using Civil3D.Bridge.DependencyInjection;
using Civil3D.Bridge.Diagnostics;
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
        Diag.Log("Initialize() entered");
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
            // Configuration is loaded before the logger is created from it, so a config failure
            // used to be invisible: nothing was written to the bridge log and the alert showed
            // only the outer message. Fall back to default settings so the failure is captured
            // in the log, and surface the whole exception chain in the alert.
            if (_options is null)
            {
                try
                {
                    Log.Logger = CreateSerilogLogger(new BridgeOptions());
                }
                catch
                {
                    // Even the fallback logger failed; the alert still carries the details.
                }
            }

            Log.Error(ex, "Civil 3D Bridge failed to initialize.");
            ShowFailureAlert(ex);
        }
    }

    private static void ShowFailureAlert(SysException ex)
    {
        try
        {
            Application.ShowAlertDialog(FailureMessageBuilder.Build(ex));
        }
        catch
        {
            // Best effort; the alert is informational only.
        }
    }

    /// <summary>Called by Civil 3D during shutdown (application context).</summary>
    public void Terminate()
    {
        Diag.Log("Terminate() entered");
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
        options.LogDirectory = ResolveLogDirectory(options.LogDirectory);

        if (string.IsNullOrWhiteSpace(options.PipeName))
        {
            options.PipeName = $"{Autodesk.Mcp.Shared.Contracts.ProtocolConstants.PipeNamePrefix}{options.Product.ToLowerInvariant()}-{Environment.ProcessId}";
        }

        return options;
    }

    private static string ResolveLogDirectory(string? logDirectory)
    {
        return string.IsNullOrWhiteSpace(logDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AutodeskMcp",
                "logs")
            : logDirectory;
    }

    private static Serilog.Core.Logger CreateSerilogLogger(BridgeOptions options)
    {
        // Empty LogDirectory (for example when falling back to defaults because the config
        // file could not be loaded) resolves to the standard %LOCALAPPDATA%\AutodeskMcp\logs.
        string logDirectory = ResolveLogDirectory(options.LogDirectory);
        Directory.CreateDirectory(logDirectory);
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.WithProperty("Bridge", options.BridgeName)
            .Enrich.WithProperty("Pid", Environment.ProcessId)
            .WriteTo.File(
                Path.Combine(logDirectory, "civil3d-bridge-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({Bridge}/{Pid}) {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
