namespace Civil3D.Bridge.Configuration;

/// <summary>
/// Raw configuration values bound from <c>bridge.config.json</c> and environment overrides.
/// Mapped to <see cref="Autodesk.Mcp.Sdk.Hosting.BridgeHostOptions"/> by the DI extension.
/// </summary>
public sealed class BridgeOptions
{
    /// <summary>Logical bridge name.</summary>
    public string BridgeName { get; set; } = "Civil3D.Bridge";

    /// <summary>Product identifier.</summary>
    public string Product { get; set; } = "Civil3D";

    /// <summary>Product version, not necessarily semantic (for example <c>2025</c>).</summary>
    public string? ProductVersion { get; set; }

    /// <summary>Semantic version of the bridge assembly.</summary>
    public string BridgeVersion { get; set; } = "1.0.0";

    /// <summary>Semantic version of the SDK the bridge is built against.</summary>
    public string SdkVersion { get; set; } = "1.0.0";

    /// <summary>Named pipe name; empty derives <c>autodesk-mcp-civil3d-&lt;pid&gt;</c>.</summary>
    public string PipeName { get; set; } = string.Empty;

    /// <summary>Maximum simultaneous pipe connections.</summary>
    public int MaxConcurrentConnections { get; set; } = 8;

    /// <summary>Products this bridge serves.</summary>
    public string[] SupportedProducts { get; set; } = { "Civil3D" };

    /// <summary>Directory for Serilog rolling files; empty derives <c>%LOCALAPPDATA%\AutodeskMcp\logs</c>.</summary>
    public string LogDirectory { get; set; } = string.Empty;

    /// <summary>True when streaming responses are supported.</summary>
    public bool SupportsStreaming { get; set; }

    /// <summary>True when <c>$/progress</c> notifications are emitted.</summary>
    public bool SupportsProgress { get; set; }

    /// <summary>True when <c>$/cancel</c> notifications are honored.</summary>
    public bool SupportsCancellation { get; set; } = true;

    /// <summary>True when the confirmation flow is supported.</summary>
    public bool SupportsConfirmation { get; set; }

    /// <summary>True when rename commands require explicit user confirmation (Phase 5B policy).</summary>
    public bool RequireConfirmationForRename { get; set; }

    /// <summary>True when batch requests are supported.</summary>
    public bool SupportsBatchRequests { get; set; }

    /// <summary>True when independent tools may run concurrently.</summary>
    public bool SupportsParallelExecution { get; set; }
}
