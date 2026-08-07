namespace Autodesk.Mcp.Shared.Contracts;

/// <summary>
/// Stable, versioned wire constants shared by the Bridge (C#) and the MCP Server protocol mirror (TypeScript).
/// Changing a value here is a breaking protocol change and must be reflected in a protocol version bump.
/// </summary>
public static class ProtocolConstants
{
    /// <summary>JSON-RPC method for the startup handshake.</summary>
    public const string Handshake = "handshake";

    /// <summary>JSON-RPC method returning the full tool catalog (<see cref="Autodesk.Mcp.Shared.Dtos.Manifest"/>).</summary>
    public const string ToolsList = "tools/list";

    /// <summary>JSON-RPC method executing a single tool.</summary>
    public const string ToolsExecute = "tools/execute";

    /// <summary>JSON-RPC method for liveness checks.</summary>
    public const string HealthPing = "health/ping";

    /// <summary>JSON-RPC method requesting a clean bridge shutdown.</summary>
    public const string Shutdown = "shutdown";

    /// <summary>JSON-RPC notification used to cancel an in-flight tool execution.</summary>
    public const string CancelNotification = "$/cancel";

    /// <summary>JSON-RPC notification used to stream progress for long-running operations.</summary>
    public const string ProgressNotification = "$/progress";

    /// <summary>Prefix for all named-pipe names owned by the platform (e.g. <c>autodesk-mcp-civil3d-12345</c>).</summary>
    public const string PipeNamePrefix = "autodesk-mcp-";

    /// <summary>Relative path (under <c>%LOCALAPPDATA%</c>) where bridges write their endpoint descriptors.</summary>
    public const string EndpointRegistryRelativePath = "AutodeskMcp/endpoints";

    /// <summary>Default per-tool execution timeout in milliseconds, used when a manifest does not override it.</summary>
    public const int DefaultToolTimeoutMilliseconds = 30_000;

    /// <summary>The current protocol major version implemented by this contract assembly.</summary>
    public const int CurrentProtocolVersionMajor = 1;

    /// <summary>The current protocol minor version implemented by this contract assembly.</summary>
    public const int CurrentProtocolVersionMinor = 0;

    /// <summary>The current protocol patch version implemented by this contract assembly.</summary>
    public const int CurrentProtocolVersionPatch = 0;

    /// <summary>The full protocol version advertised by this contract assembly (semantic version).</summary>
    public static readonly VersionInformation CurrentProtocolVersion =
        new(CurrentProtocolVersionMajor, CurrentProtocolVersionMinor, CurrentProtocolVersionPatch);
}
