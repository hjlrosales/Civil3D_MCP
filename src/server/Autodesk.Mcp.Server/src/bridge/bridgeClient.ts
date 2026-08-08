import { EventEmitter } from 'node:events';
import { ErrorCode, type BridgeInformation, type ClientCapabilities, type EndpointDescriptor, type ExecuteToolRequest, type HandshakeResponse, type Manifest, type ProgressNotification, type ResponseEnvelope, type ToolManifest } from '../protocol/types.js';
import { CurrentProtocolVersion, Method } from '../protocol/constants.js';
import { BridgeConnection, BridgeConnectionError } from '../transport/bridgeConnection.js';
import type { IncomingNotification } from '../transport/bridgeConnection.js';

export interface BridgeClientOptions {
  /** The discovery descriptor of the bridge to talk to. */
  endpoint: EndpointDescriptor;
  /** Client identity reported to the bridge during the handshake. */
  clientName: string;
  clientVersion?: string;
  /** Capabilities advertised to the bridge during the handshake. */
  capabilities?: ClientCapabilities;
  /** Per-request timeout in milliseconds. */
  requestTimeoutMs?: number;
  /** Connection timeout in milliseconds. */
  connectTimeoutMs?: number;
  /** Enables the $/cancel forwarding seam (default true). */
  supportsCancellation?: boolean;
}

export interface ManifestChange {
  added: ToolManifest[];
  removed: ToolManifest[];
  changed: ToolManifest[];
}

export interface ExecuteOptions {
  correlationId?: string;
  sessionId?: string;
  timeoutMs?: number;
  confirm?: boolean;
}

/**
 * A high-level client bound to one discovered bridge: performs the handshake, caches the tool
 * manifest (with diff detection for re-registration), executes tools, and re-exposes bridge
 * notifications ($/progress) as typed events.
 */
export class BridgeClient extends EventEmitter {
  private readonly options: Required<Pick<BridgeClientOptions, 'clientName'>> &
    Omit<BridgeClientOptions, 'clientName'>;
  private connection: BridgeConnection | null = null;
  private sessionId: string | null = null;
  private bridgeInfo: BridgeInformation | null = null;
  private manifest: Manifest | null = null;

  constructor(options: BridgeClientOptions) {
    super();
    this.options = { ...options };
  }

  /** The endpoint this client is bound to. */
  get endpoint(): EndpointDescriptor {
    return this.options.endpoint;
  }

  /** The session id issued by the bridge after a successful handshake. */
  get currentSessionId(): string | null {
    return this.sessionId;
  }

  /** The bridge information reported during the handshake. */
  get information(): BridgeInformation | null {
    return this.bridgeInfo;
  }

  /** The currently cached manifest, or null before the first tools/list. */
  get currentManifest(): Manifest | null {
    return this.manifest;
  }

  /** True when the underlying pipe is connected. */
  get connected(): boolean {
    return this.connection !== null && this.connection.connected;
  }

  /**
   * Connects to the pipe and performs the protocol handshake. The client refuses to talk to a
   * bridge whose protocol major version is not 1 (mirrors the C# handshake check).
   */
  async connect(): Promise<HandshakeResponse> {
    if (this.connection !== null) {
      throw new BridgeConnectionError('The bridge client is already connected.', ErrorCode.E_BRIDGE_UNAVAILABLE);
    }

    const connection = new BridgeConnection({
      pipeName: this.options.endpoint.pipeName,
      connectTimeoutMs: this.options.connectTimeoutMs,
      requestTimeoutMs: this.options.requestTimeoutMs,
    });
    connection.on('notification', (notification: IncomingNotification) => this.onNotification(notification));
    connection.on('close', () => this.onConnectionClosed());
    connection.on('error', (error: Error) => this.emit('error', error));

    await connection.connect();
    this.connection = connection;

    const response = await connection.request(Method.Handshake, {
      protocolVersion: CurrentProtocolVersion,
      clientName: this.options.clientName,
      clientVersion: this.options.clientVersion,
      capabilities: this.options.capabilities,
    });
    if (!response.success) {
      connection.close();
      this.connection = null;
      throw new BridgeConnectionError(
        response.message || 'The bridge rejected the handshake.',
        response.errorCode ?? ErrorCode.E_INTERNAL,
      );
    }

    const handshake = response.data as HandshakeResponse;
    if (typeof handshake?.sessionId !== 'string' || handshake.sessionId.length === 0) {
      connection.close();
      this.connection = null;
      throw new BridgeConnectionError('The bridge handshake did not return a session id.', ErrorCode.E_INVALID_REQUEST);
    }

    this.sessionId = handshake.sessionId;
    this.bridgeInfo = handshake.bridge ?? null;
    this.emit('connected', handshake);
    return handshake;
  }

  /**
   * Loads the full tool catalog via tools/list and caches it. Emits 'manifest' with the change
   * description when the catalog differs from the previously cached one (diff-caching).
   */
  async loadManifest(): Promise<Manifest> {
    const response = await this.request(Method.ToolsList, undefined);
    if (!response.success) {
      throw new BridgeConnectionError(
        response.message || 'tools/list failed.',
        response.errorCode ?? ErrorCode.E_INTERNAL,
      );
    }

    const manifest = response.data as Manifest;
    if (!Array.isArray(manifest?.tools)) {
      throw new BridgeConnectionError('tools/list returned a malformed manifest.', ErrorCode.E_INVALID_REQUEST);
    }

    const change = diffManifests(this.manifest, manifest);
    this.manifest = manifest;
    this.emit('manifest', { manifest, change });
    return manifest;
  }

  /** Executes a tool and returns the raw bridge envelope (business failures are returned, not thrown). */
  async execute(tool: string, args: unknown, options: ExecuteOptions = {}): Promise<ResponseEnvelope> {
    const payload: ExecuteToolRequest = { tool, arguments: args };
    if (options.timeoutMs !== undefined) {
      payload.timeoutMs = options.timeoutMs;
    }
    if (options.confirm !== undefined) {
      payload.confirm = options.confirm;
    }

    return this.request(Method.ToolsExecute, payload, {
      correlationId: options.correlationId,
      sessionId: options.sessionId ?? this.sessionId ?? undefined,
      timeoutMs: options.timeoutMs,
    });
  }

  /** Cancels an in-flight execution by correlation id. */
  cancel(correlationId: string, reason?: string): void {
    if (this.connection === null || !this.connection.connected) {
      return;
    }
    this.connection.cancel(correlationId, reason);
  }

  /** Performs a liveness check against the bridge. */
  async ping(): Promise<void> {
    await this.requireConnection().ping(this.sessionId ?? undefined);
  }

  /** Requests a clean bridge shutdown (best-effort). */
  async shutdown(): Promise<void> {
    const connection = this.connection;
    if (connection !== null && connection.connected) {
      await connection.shutdown(this.sessionId ?? undefined);
    }
  }

  /** Closes the pipe connection; a fresh client is required to reconnect. */
  close(): void {
    this.connection?.close();
    this.connection = null;
    this.sessionId = null;
  }

  private request(method: string, params?: unknown, options?: { correlationId?: string; sessionId?: string; timeoutMs?: number }): Promise<ResponseEnvelope> {
    return this.requireConnection().request(method, params, {
      ...options,
      sessionId: options?.sessionId ?? this.sessionId ?? undefined,
    });
  }

  private requireConnection(): BridgeConnection {
    if (this.connection === null || !this.connection.connected) {
      throw new BridgeConnectionError('The bridge is not connected.', ErrorCode.E_BRIDGE_UNAVAILABLE);
    }
    return this.connection;
  }

  private onNotification(notification: IncomingNotification): void {
    if (notification.method === '$/progress') {
      this.emit('progress', notification.params as ProgressNotification);
      return;
    }
    this.emit('notification', notification);
  }

  private onConnectionClosed(): void {
    this.sessionId = null;
    this.emit('close');
  }
}

/**
 * Diffs two manifests by tool name and version. A tool is 'added' when it is new, 'removed'
 * when it disappeared, and 'changed' when its version (or schema) changed between loads.
 */
export function diffManifests(previous: Manifest | null, current: Manifest): ManifestChange {
  const previousByName = new Map<string, ToolManifest>(
    (previous?.tools ?? []).map((tool) => [tool.name, tool]),
  );

  const added: ToolManifest[] = [];
  const changed: ToolManifest[] = [];
  const removed: ToolManifest[] = [];

  for (const tool of current.tools) {
    const prior = previousByName.get(tool.name);
    if (prior === undefined) {
      added.push(tool);
    } else if (prior.version !== tool.version) {
      changed.push(tool);
    }
    previousByName.delete(tool.name);
  }
  for (const tool of previousByName.values()) {
    removed.push(tool);
  }

  return { added, removed, changed };
}
