import { EventEmitter } from 'node:events';
import { BridgeClient, type ManifestChange } from './bridge/bridgeClient.js';
import { EndpointMonitor } from './discovery/monitor.js';
import { selectEndpoint, type EndpointPreferences, type EndpointLogger } from './discovery/endpointStore.js';
import { type ClientCapabilities, type EndpointDescriptor, type Manifest, type ProgressNotification } from './protocol/types.js';

export interface BridgeManagerOptions {
  endpointsDir: string;
  /** Endpoint preferences (preferred product / preferred bridge name). */
  preferences?: EndpointPreferences;
  /** Client identity reported to the bridge during the handshake. */
  clientName: string;
  clientVersion?: string;
  /** Capabilities advertised to the bridge during the handshake. */
  capabilities?: ClientCapabilities;
  /** Polling interval for the endpoints registry. */
  endpointsPollIntervalMs?: number;
  /** Base reconnect delay; doubled on each failed attempt. */
  reconnectDelayMs?: number;
  /** Maximum reconnect attempts before giving up (0 = keep trying forever). */
  maxReconnectAttempts?: number;
  /** Per-request timeout in milliseconds. */
  requestTimeoutMs?: number;
  /** Heartbeat (health/ping) interval while connected; 0 disables heartbeats. */
  heartbeatIntervalMs?: number;
  logger?: EndpointLogger;
}

export type BridgeStatus = 'discovering' | 'connecting' | 'connected' | 'reconnecting' | 'offline';

/**
 * Owns bridge lifecycle: watches the endpoints registry, selects the preferred bridge (supporting
 * multiple running instances), connects, loads the manifest, reconnects with exponential backoff
 * after failures, and pings periodically to detect silent hangs. Emits 'status', 'endpoint',
 * 'manifest' and 'progress' for the MCP adapter.
 */
export class BridgeManager extends EventEmitter {
  private readonly options: Required<
    Pick<BridgeManagerOptions, 'endpointsDir' | 'clientName' | 'endpointsPollIntervalMs' | 'reconnectDelayMs' | 'maxReconnectAttempts' | 'requestTimeoutMs' | 'heartbeatIntervalMs'>
  > &
    Pick<BridgeManagerOptions, 'preferences' | 'clientVersion' | 'capabilities' | 'logger'>;

  private readonly monitor: EndpointMonitor;
  private client: BridgeClient | null = null;
  private endpoint: EndpointDescriptor | null = null;
  private status: BridgeStatus = 'discovering';
  private stopped = false;
  private attempt = 0;
  private reconnectTimer: NodeJS.Timeout | null = null;
  private heartbeatTimer: NodeJS.Timeout | null = null;

  constructor(options: BridgeManagerOptions) {
    super();
    this.options = {
      endpointsDir: options.endpointsDir,
      clientName: options.clientName,
      endpointsPollIntervalMs: options.endpointsPollIntervalMs ?? 3_000,
      reconnectDelayMs: options.reconnectDelayMs ?? 1_000,
      maxReconnectAttempts: options.maxReconnectAttempts ?? 10,
      requestTimeoutMs: options.requestTimeoutMs ?? 30_000,
      heartbeatIntervalMs: options.heartbeatIntervalMs ?? 15_000,
      preferences: options.preferences,
      clientVersion: options.clientVersion,
      capabilities: options.capabilities,
      logger: options.logger,
    };

    this.monitor = new EndpointMonitor({
      endpointsDir: options.endpointsDir,
      pollIntervalMs: this.options.endpointsPollIntervalMs,
      logger: options.logger,
    });
    this.monitor.on('update', (endpoints: EndpointDescriptor[]) => this.onEndpointsChanged(endpoints));
  }

  /** The currently connected bridge client, or null. */
  getBridge(): BridgeClient | null {
    return this.client;
  }

  /** The currently selected endpoint, or null. */
  getEndpoint(): EndpointDescriptor | null {
    return this.endpoint;
  }

  /** The current connection status. */
  getStatus(): BridgeStatus {
    return this.status;
  }

  /** The latest loaded manifest, or null. */
  getManifest(): Manifest | null {
    return this.client?.currentManifest ?? null;
  }

  /** Starts endpoint monitoring (connects as soon as a bridge is discovered). */
  start(): void {
    this.stopped = false;
    this.monitor.start();
  }

  /** Stops monitoring, closes the connection and clears timers. Idempotent. */
  stop(): void {
    if (this.stopped) {
      return;
    }
    this.stopped = true;
    this.monitor.stop();
    this.clearReconnectTimer();
    this.clearHeartbeatTimer();
    this.client?.close();
    this.client = null;
    this.endpoint = null;
    this.setStatus('offline');
  }

  private onEndpointsChanged(endpoints: EndpointDescriptor[]): void {
    const selected = selectEndpoint(endpoints, this.options.preferences);
    if (selected === null) {
      if (this.client !== null) {
        this.log('warn', 'The connected bridge disappeared from the registry; disconnecting.');
        this.disconnect();
      }
      return;
    }

    const sameEndpoint = this.endpoint !== null &&
      this.endpoint.pipeName === selected.pipeName &&
      this.endpoint.pid === selected.pid;
    if (sameEndpoint) {
      return;
    }

    this.log('info', 'Selected endpoint %s (%s, pipe %s).', selected.bridgeName, selected.product, selected.pipeName);
    this.disconnect();
    this.endpoint = selected;
    void this.connectTo(selected);
  }

  private async connectTo(endpoint: EndpointDescriptor): Promise<void> {
    this.setStatus(this.attempt === 0 ? 'connecting' : 'reconnecting');
    this.log('info', 'Connecting to bridge on pipe %s (attempt %d).', endpoint.pipeName, this.attempt + 1);

    const client = new BridgeClient({
      endpoint,
      clientName: this.options.clientName,
      clientVersion: this.options.clientVersion,
      capabilities: this.options.capabilities,
      requestTimeoutMs: this.options.requestTimeoutMs,
    });
    client.on('close', () => this.onClientClosed());
    client.on('error', (error: Error) => this.log('warn', 'Bridge client error: %s', error.message));
    client.on('progress', (progress: ProgressNotification) => this.emit('progress', progress));
    client.on('manifest', (event: { manifest: Manifest; change: ManifestChange }) => {
      this.attempt = 0;
      this.emit('manifest', event.manifest, event.change);
      this.log(
        'info',
        'Loaded manifest with %d tools (added %d, changed %d, removed %d).',
        event.manifest.tools.length,
        event.change.added.length,
        event.change.changed.length,
        event.change.removed.length,
      );
    });

    try {
      await client.connect();
      this.client = client;
      this.attempt = 0;
      this.setStatus('connected');
      this.log(
        'info',
        'Connected to %s (session %s, protocol %s).',
        client.information?.bridgeName ?? endpoint.bridgeName,
        client.currentSessionId ?? '',
        client.information?.protocolVersion ?? '',
      );
      this.startHeartbeat();
      await client.loadManifest();
      this.emit('endpoint', endpoint);
    } catch (error) {
      client.close();
      this.log('warn', 'Connection to %s failed: %s', endpoint.pipeName, error instanceof Error ? error.message : String(error));
      this.scheduleReconnect();
    }
  }

  private onClientClosed(): void {
    if (this.stopped) {
      return;
    }
    this.log('warn', 'Connection to %s was lost; reconnecting.', this.endpoint?.pipeName ?? 'the bridge');
    this.client = null;
    this.clearHeartbeatTimer();
    this.setStatus('reconnecting');
    this.scheduleReconnect();
  }

  private scheduleReconnect(): void {
    if (this.stopped) {
      return;
    }
    this.clearReconnectTimer();

    if (this.options.maxReconnectAttempts > 0 && this.attempt >= this.options.maxReconnectAttempts) {
      this.log('warn', 'Giving up after %d reconnect attempts.', this.attempt);
      this.setStatus('offline');
      return;
    }

    const delay = this.options.reconnectDelayMs * Math.pow(2, Math.min(this.attempt, 8));
    this.attempt += 1;
    this.setStatus('reconnecting');
    this.log('info', 'Reconnecting in %d ms (attempt %d).', delay, this.attempt);
    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = null;
      if (this.stopped) {
        return;
      }
      if (this.endpoint !== null) {
        void this.connectTo(this.endpoint);
      }
    }, delay);
  }

  private startHeartbeat(): void {
    this.clearHeartbeatTimer();
    if (this.options.heartbeatIntervalMs <= 0) {
      return;
    }
    this.heartbeatTimer = setInterval(() => {
      const client = this.client;
      if (client === null || !client.connected) {
        return;
      }
      client.ping().catch((error: unknown) => {
        this.log('warn', 'Heartbeat failed: %s', error instanceof Error ? error.message : String(error));
        this.onClientClosed();
      });
    }, this.options.heartbeatIntervalMs);
  }

  private disconnect(): void {
    this.clearReconnectTimer();
    this.clearHeartbeatTimer();
    this.client?.close();
    this.client = null;
  }

  private clearReconnectTimer(): void {
    if (this.reconnectTimer !== null) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
  }

  private clearHeartbeatTimer(): void {
    if (this.heartbeatTimer !== null) {
      clearInterval(this.heartbeatTimer);
      this.heartbeatTimer = null;
    }
  }

  private setStatus(status: BridgeStatus): void {
    if (this.status === status) {
      return;
    }
    this.status = status;
    this.emit('status', status);
  }

  private log(level: 'info' | 'warn' | 'debug', message: string, ...args: any[]): void {
    this.options.logger?.[level](message, ...args);
  }
}
