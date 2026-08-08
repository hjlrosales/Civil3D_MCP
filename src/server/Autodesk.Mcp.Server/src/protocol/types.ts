/**
 * TypeScript mirrors of the shared wire contracts. Field names and semantics match the C#
 * records in Autodesk.Mcp.Shared one-to-one (camelCase, nulls omitted, versions as SemVer
 * strings, enums as their exact member names). Unknown properties are tolerated on read.
 */

/** A JSON-RPC 2.0 request/notification id: number or string. */
export type JsonRpcId = number | string;

/**
 * The JSON-RPC 2.0 request envelope used for every bridge method call. Notifications
 * (no reply expected) omit the id entirely.
 */
export interface RequestEnvelope {
  jsonrpc?: '2.0';
  /** The protocol method name; see the Method constants. */
  method: string;
  /** Present on requests, absent on notifications. */
  id?: JsonRpcId;
  /** Positional or named parameters for the method. */
  params?: unknown;
  /** End-to-end correlation identifier propagated into logs and responses. */
  correlationId?: string;
  /** The session identifier established at handshake. */
  sessionId?: string;
  /** Requested execution timeout in milliseconds; the bridge applies its own maximum. */
  timeoutMilliseconds?: number;
  /** UTC timestamp captured by the caller when the request was sent. */
  clientRequestedAtUtc?: string;
}

/** The standard, frozen response envelope returned for every tool execution. */
export interface ResponseEnvelope {
  /** True when the operation completed successfully. */
  success: boolean;
  /** Human-readable result or failure message; never carries exception details. */
  message?: string;
  /** Wall-clock execution time reported by the bridge, in milliseconds. */
  executionTime?: number;
  /** Stable error code; E_UNKNOWN on success. */
  errorCode?: string;
  /** Correlation identifier echoed from the originating request. */
  correlationId?: string;
  /** Session identifier echoed from the originating request. */
  sessionId?: string;
  /** Raw result payload (the tool output). */
  data?: unknown;
}

/** Protocol-level error object used when a response carries a transport/protocol failure. */
export interface ErrorEnvelope {
  /** The stable error code; see ErrorCode. */
  errorCode: string;
  /** A safe, user-visible message. Never contains exception details or stack traces. */
  message: string;
  /** Optional structured details that help diagnose the failure. */
  details?: unknown;
  /** Correlation identifier of the failing request, when known. */
  correlationId?: string;
  /** Session identifier of the failing request, when known. */
  sessionId?: string;
  /** UTC timestamp at which the error was produced. */
  occurredAtUtc: string;
}

/** A progress notification streamed from the bridge while a long-running tool executes. */
export interface ProgressNotification {
  /** Correlation identifier of the in-flight operation this progress belongs to. */
  correlationId: string;
  /** Session identifier of the in-flight operation, when known. */
  sessionId?: string;
  /** The name of the tool that is reporting progress. */
  toolName?: string;
  /** Completion percentage in the range 0..100. */
  percent: number;
  /** Short stage label (for example rebuilding corridor). */
  stage?: string;
  /** Optional human-readable detail for the current stage. */
  message?: string;
  /** UTC timestamp at which this progress update was produced. */
  timestampUtc: string;
}

/** A request to cancel an in-flight tool execution (sent as the $/cancel notification). */
export interface CancellationRequest {
  /** Correlation identifier of the operation to cancel. */
  correlationId: string;
  /** Optional human-readable reason for the cancellation. */
  reason?: string;
  /** UTC timestamp at which the cancellation was requested. */
  requestedAtUtc: string;
}

/** A liveness message exchanged on health/ping. */
export interface Heartbeat {
  /** UTC timestamp at which the heartbeat was produced. */
  timestampUtc: string;
  /** Session identifier of the bridge being pinged, when applicable. */
  sessionId?: string;
  /** The operating system process id of the bridge, when known. */
  processId?: number;
}

/** The capabilities a bridge advertises (discovery + handshake). */
export interface BridgeCapabilities {
  supportsStreaming?: boolean;
  supportsProgress?: boolean;
  supportsCancellation?: boolean;
  supportsConfirmation?: boolean;
  supportsBatchRequests?: boolean;
  supportsParallelExecution?: boolean;
  supportedProtocolVersion?: string;
  supportedProducts?: string[];
}

/** Optional capabilities advertised by the client (the MCP server) during the handshake. */
export interface ClientCapabilities {
  supportsConfirmation?: boolean;
  supportsProgress?: boolean;
  supportsCancellation?: boolean;
  supportsBatchRequests?: boolean;
  supportsParallelExecution?: boolean;
}

/** The discovery record a bridge writes to the endpoints directory. */
export interface EndpointDescriptor {
  /** Logical bridge name (for example Civil3D.Bridge). */
  bridgeName: string;
  /** Product identifier (for example Civil3D). */
  product: string;
  /** Product version, not necessarily semantic (for example 2026). */
  productVersion?: string;
  /** Version of the bridge that owns this endpoint. */
  bridgeVersion: string;
  /** Version of the SDK assembly the bridge is built against. */
  sdkVersion: string;
  /** Version of the wire protocol the bridge speaks. */
  protocolVersion: string;
  /** The named pipe the bridge is listening on. */
  pipeName: string;
  /** The operating system process id of the bridge (wire name pid). */
  pid: number;
  /** UTC timestamp of bridge startup (wire name startedUtc). */
  startedUtc: string;
  /** UTC timestamp of the most recent heartbeat, when the bridge reports them. */
  lastHeartbeatAtUtc?: string;
  /** The capabilities the bridge offers, mirrored here for pre-filtering. */
  capabilities?: BridgeCapabilities;
}

/** Descriptive metadata a bridge reports about itself during the handshake. */
export interface BridgeInformation {
  bridgeName: string;
  product: string;
  productVersion?: string;
  bridgeVersion: string;
  sdkVersion: string;
  protocolVersion: string;
  capabilities?: BridgeCapabilities;
}

/** The payload of the handshake method sent by the client to the bridge. */
export interface HandshakeRequest {
  /** The semantic protocol version the client speaks; 0.0.0 means not provided. */
  protocolVersion: string;
  /** Name of the connecting client (for example Autodesk.MCP.Server). */
  clientName: string;
  /** Version of the connecting client, when known. */
  clientVersion?: string;
  /** Reserved for future authentication. */
  authenticationToken?: string;
  /** Optional capabilities the client supports. */
  capabilities?: ClientCapabilities;
}

/** The bridge answer to a handshake. */
export interface HandshakeResponse {
  /** The protocol version agreed upon for this connection. */
  protocolVersion: string;
  /** The session identifier the client must echo on every subsequent request. */
  sessionId: string;
  /** Descriptive metadata about the bridge that accepted the connection. */
  bridge?: BridgeInformation;
  /** Optional human-readable note. */
  message?: string;
}

/** An optional worked example attached to a tool manifest. */
export interface ToolExample {
  name?: string;
  description?: string;
  input?: unknown;
  output?: unknown;
}

/** The immutable runtime description of one tool, served by tools/list. */
export interface ToolManifest {
  /** The stable machine identifier used on the wire (for example list_alignments). */
  name: string;
  /** Human-friendly label shown to users. */
  displayName: string;
  /** Markdown-capable description of what the tool does. */
  description: string;
  /** The functional category the tool belongs to (ToolCategory member name). */
  category?: string;
  /** Semantic version of this tool contract. */
  version: string;
  /** The permission level required to invoke this tool (ToolPermission member name). */
  permission?: string;
  /** The risk level associated with invoking this tool (ToolRisk member name). */
  risk?: string;
  /** Maximum execution time in milliseconds before the bridge cancels the tool. */
  timeoutMilliseconds: number;
  /** True when the tool emits $/progress notifications during execution. */
  supportsProgress?: boolean;
  /** True when the tool cooperates with $/cancel notifications. */
  supportsCancellation?: boolean;
  /** True when the tool can stream partial results before completion. */
  supportsStreaming?: boolean;
  /** JSON Schema describing valid inputs. */
  inputSchema: Record<string, unknown>;
  /** JSON Schema describing the output. */
  outputSchema?: Record<string, unknown>;
  /** Optional worked examples of inputs and outputs. */
  examples?: ToolExample[];
  /** Free-form classification tags used for filtering and discovery. */
  tags?: string[];
  /** True when the tool is deprecated and should be hidden from new clients. */
  deprecated?: boolean;
}

/** The complete tool catalog returned by tools/list. */
export interface Manifest {
  /** Schema version of this manifest document. */
  schemaVersion: number;
  /** The protocol version under which this catalog was produced. */
  protocolVersion: string;
  /** UTC timestamp at which the catalog was generated. */
  generatedAtUtc: string;
  /** All tools served by this bridge. */
  tools: ToolManifest[];
}

/** The wire payload of a tools/execute request. */
export interface ExecuteToolRequest {
  /** The name of the tool to execute. */
  tool: string;
  /** Raw tool arguments. */
  arguments?: unknown;
  /** Optional execution timeout override in milliseconds. */
  timeoutMs?: number;
  /** Explicit confirmation flag for editing tools (reserved; policy enforced bridge-side). */
  confirm?: boolean;
}

/** The stable error codes used on the wire (exact member names, mirrored from C#). */
export const ErrorCode = {
  E_UNKNOWN: 'E_UNKNOWN',
  E_TIMEOUT: 'E_TIMEOUT',
  E_CANCELLED: 'E_CANCELLED',
  E_INVALID_REQUEST: 'E_INVALID_REQUEST',
  E_INVALID_PARAMETERS: 'E_INVALID_PARAMETERS',
  E_PERMISSION_DENIED: 'E_PERMISSION_DENIED',
  E_CONFIRMATION_REQUIRED: 'E_CONFIRMATION_REQUIRED',
  E_NO_ACTIVE_DOCUMENT: 'E_NO_ACTIVE_DOCUMENT',
  E_TRANSACTION_FAILED: 'E_TRANSACTION_FAILED',
  E_OBJECT_NOT_FOUND: 'E_OBJECT_NOT_FOUND',
  E_SERIALIZATION: 'E_SERIALIZATION',
  E_INTERNAL: 'E_INTERNAL',
  E_VALIDATION_FAILED: 'E_VALIDATION_FAILED',
  E_BRIDGE_UNAVAILABLE: 'E_BRIDGE_UNAVAILABLE',
} as const;

export type ErrorCodeValue = (typeof ErrorCode)[keyof typeof ErrorCode];
