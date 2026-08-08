/**
 * Stable, versioned wire constants shared by the Bridge (C#) and the MCP Server protocol
 * mirror (TypeScript). Mirrors Autodesk.Mcp.Shared.Contracts.ProtocolConstants exactly; changing
 * a value here is a breaking protocol change and must be reflected in a protocol version bump.
 */

export const Method = {
  Handshake: 'handshake',
  ToolsList: 'tools/list',
  ToolsExecute: 'tools/execute',
  HealthPing: 'health/ping',
  Shutdown: 'shutdown',
} as const;

/** JSON-RPC notification names (no reply expected). */
export const Notification = {
  Cancel: '$/cancel',
  Progress: '$/progress',
} as const;

/** Prefix for all named-pipe names owned by the platform (e.g. autodesk-mcp-civil3d-12345). */
export const PipeNamePrefix = 'autodesk-mcp-';

/** Relative path (under %LOCALAPPDATA%) where bridges write their endpoint descriptors. */
export const EndpointRegistryRelativePath = 'AutodeskMcp/endpoints';

/** Default per-tool execution timeout in milliseconds, used when a manifest does not override it. */
export const DefaultToolTimeoutMilliseconds = 30_000;

/** The protocol version implemented by the current contract assembly (SemVer string). */
export const CurrentProtocolVersion = '1.0.0';

/** Hard guard against oversized wire messages (mirrors NdjsonProtocol.MaxMessageLength). */
export const MaxMessageLength = 4 * 1024 * 1024;

/** The default endpoints directory used when %LOCALAPPDATA% is not resolvable. */
export const DefaultEndpointsDir = 'AutodeskMcp/endpoints';
