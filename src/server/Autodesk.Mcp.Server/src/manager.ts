import { EventEmitter } from 'node:events';
import { BridgeClient, type ManifestChange } from './bridge/bridgeClient.js';
import { EndpointMonitor } from './discovery/monitor.js';
import { endpointFingerprint, selectEndpoint, type EndpointPreferences, type EndpointLogger } from './discovery/endpointStore.js';
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
  /**
   * Reconnect attempts in one burst before the endpoint is parked and retried at the slower
   * rediscovery cadence (0 = keep retrying in the same burst forever).
   */
  maxReconnectAttempts?: number;
  /**
   * How long a burst-exhausted endpoint is left alone before discovery retries it. Bounds the
   * retry rate against a bridge that is registered but permanently unreachable.
   */
  retryCooldownMs?: number;
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
 * 'manifest', 'manifestCleared' and 'progress' for the MCP adapter.
 *
 * The manager never reaches a terminal state while the process is running: an endpoint that
 * cannot be reached is parked after a burst of attempts and retried at the rediscovery cadence,
 * so Civil 3D can come and go all day without the user restarting anything.
 */
export class BridgeManager extends EventEmitter {
  private readonly options: Required<
    Pick<BridgeManagerOptions, 'endpointsDir' | 'clientName' | 'endpointsPollIntervalMs' | 'reconnectDelayMs' | 'maxReconnectAttempts' | 'retryCooldownMs' | 'requestTimeoutMs' | 'heartbeatIntervalMs'>
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
  /** True while connectTo() is in flight, so a poll cannot start a second connection. */
  private connecting = false;
  /** An endpoint whose retry burst was exhausted, and the earliest time to try it again. */
  private parked: { fingerprint: string; retryAtMs: number } | null = null;
  /** True once a manifest has been published, so 'manifestCleared' is only emitted when needed. */
  private manifestPublished = false;

  constructor(options: BridgeManagerOptions) {
    super();
    this.options = {
      endpointsDir: options.endpointsDir,
      clientName: options.clientName,
      endpointsPollIntervalMs: options.endpointsPollIntervalMs ?? 3_000,
      reconnectDelayMs: options.reconnectDelayMs ?? 1_000,
      maxReconnectAttempts: options.maxReconnectAttempts ?? 10,
      retryCooldownMs: options.retryCooldownMs ?? 30_000,
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
    this.monitor.on('snapshot', (endpoints: EndpointDescriptor[]) => this.onEndpointsSnapshot(endpoints));
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
    this.log('info', 'Searching for bridge endpoints in %s (every %d ms).', this.options.endpointsDir, this.options.endpointsPollIntervalMs);
    this.setStatus('discovering');
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
    this.parked = null;
    this.setStatus('offline');
    this.log('info', 'Bridge monitoring stopped.');
  }

  /**
   * Evaluates the endpoint registry on every poll and drives the connection state machine.
   * Running on every poll (rather than only on registry changes) is what lets the manager recover
   * from a failed connection to an endpoint that is still registered: the descriptor is unchanged,
   * so a change-only trigger would never fire again.
   */
  private onEndpointsSnapshot(endpoints: EndpointDescriptor[]): void {
    if (this.stopped) {
      return;
    }

    const selected = selectEndpoint(endpoints, this.options.preferences);
    if (selected === null) {
      this.onNoEndpointAvailable(endpoints);
      return;
    }

    const fingerprint = endpointFingerprint(selected);
    const isCurrent = this.endpoint !== null && endpointFingerprint(this.endpoint) === fingerprint;

    if (isCurrent) {
      // Connected, connecting, or waiting on a scheduled retry: leave it alone.
      if (this.client !== null || this.connecting || this.reconnectTimer !== null) {
        return;
      }
      // The retry burst was exhausted. The bridge is still registered and its process is alive,
      // so try again once the cooldown expires instead of giving up for the rest of the session.
      if (this.parked !== null && this.parked.fingerprint === fingerprint) {
        if (Date.now() < this.parked.retryAtMs) {
          return;
        }
        this.log('info', 'Retrying bridge %s after the reconnect cooldown.', selected.pipeName);
        this.parked = null;
        this.attempt = 0;
      }
      void this.connectTo(selected);
      return;
    }

    // A different bridge became preferred (Civil 3D restarted, or a newer instance appeared).
    if (this.endpoint !== null) {
      this.log('info', 'Switching from bridge %s to %s.', this.endpoint.pipeName, selected.pipeName);
    }
    this.log(
      'info',
      'Endpoint discovered: %s (%s %s, pipe %s, pid %d).',
      selected.bridgeName,
      selected.product,
      selected.productVersion ?? 'unknown',
      selected.pipeName,
      selected.pid,
    );
    this.disconnect();
    this.parked = null;
    this.attempt = 0;
    this.endpoint = selected;
    void this.connectTo(selected);
  }

  /** No usable endpoint remains: tear down and go back to discovering. */
  private onNoEndpointAvailable(endpoints: EndpointDescriptor[]): void {
    if (this.endpoint === null && this.client === null) {
      return; // Already idle; nothing to report.
    }

    if (endpoints.length > 0) {
      this.log('info', 'Endpoint(s) present but none is usable (dead process or filtered out by preferences); waiting.');
    } else {
      this.log('info', 'The bridge endpoint disappeared (Civil 3D closed); waiting for it to return.');
    }
    this.disconnect();
    this.endpoint = null;
    this.parked = null;
    this.attempt = 0;
    this.setStatus('discovering');
    this.publishManifestCleared();
  }

  private async connectTo(endpoint: EndpointDescriptor): Promise<void> {
    if (this.stopped || this.connecting || this.client !== null) {
      return; // Never run two connection attempts against one manager.
    }
    this.connecting = true;
    this.setStatus(this.attempt === 0 ? 'connecting' : 'reconnecting');
    this.log('info', 'Connecting to bridge on pipe %s (attempt %d).', endpoint.pipeName, this.attempt + 1);

    const client = new BridgeClient({
      endpoint,
      clientName: this.options.clientName,
      clientVersion: this.options.clientVersion,
      capabilities: this.options.capabilities,
      requestTimeoutMs: this.options.requestTimeoutMs,
    });
    // Only the client that is currently active may drive the state machine; a close event from a
    // superseded client must not trigger a reconnect.
    client.on('close', () => this.onClientClosed(client));
    client.on('error', (error: Error) => this.log('warn', 'Bridge client error: %s', error.message));
    client.on('progress', (progress: ProgressNotification) => this.emit('progress', progress));
    client.on('manifest', (event: { manifest: Manifest; change: ManifestChange }) => {
      this.attempt = 0;
      this.manifestPublished = true;
      this.emit('manifest', event.manifest, event.change);
      this.log(
        'info',
        'Manifest loaded from %s: %d tool(s) available (added %d, changed %d, removed %d).',
        endpoint.pipeName,
        event.manifest.tools.length,
        event.change.added.length,
        event.change.changed.length,
        event.change.removed.length,
      );
    });

    try {
      await client.connect();
      if (this.stopped) {
        client.close(); // stop() ran while the handshake was in flight.
        return;
      }
      this.client = client;
      this.attempt = 0;
      this.parked = null;
      this.setStatus('connected');
      this.log(
        'info',
        'Handshake succeeded with %s (session %s, protocol %s, pid %d).',
        client.information?.bridgeName ?? endpoint.bridgeName,
        client.currentSessionId ?? '',
        client.information?.protocolVersion ?? '',
        endpoint.pid,
      );
      this.startHeartbeat();
      await client.loadManifest();
      this.emit('endpoint', endpoint);
    } catch (error) {
      // The client may already be installed (a failure in loadManifest happens after connect);
      // drop it so the state machine does not keep a half-initialised connection.
      if (this.client === client) {
        this.client = null;
        this.clearHeartbeatTimer();
      }
      client.close();
      this.log('warn', 'Connection to %s failed: %s', endpoint.pipeName, error instanceof Error ? error.message : String(error));
      this.connecting = false;
      this.scheduleReconnect();
      return;
    } finally {
      this.connecting = false;
    }
  }

  private onClientClosed(client: BridgeClient): void {
    if (this.stopped || this.client !== client) {
      return; // Already torn down, or a superseded client closing late.
    }
    this.log('warn', 'Bridge %s disconnected; scheduling reconnect.', this.endpoint?.pipeName ?? 'connection');
    this.client = null;
    this.clearHeartbeatTimer();
    this.setStatus('reconnecting');
    this.scheduleReconnect();
  }

  /**
   * Schedules the next attempt with exponential backoff. When the burst budget is spent the
   * endpoint is parked rather than abandoned: discovery retries it after the cooldown, so a bridge
   * that becomes reachable later is picked up without restarting the server.
   */
  private scheduleReconnect(): void {
    if (this.stopped) {
      return;
    }
    this.clearReconnectTimer();

    if (this.options.maxReconnectAttempts > 0 && this.attempt >= this.options.maxReconnectAttempts) {
      const cooldown = this.options.retryCooldownMs;
      if (this.endpoint !== null) {
        this.parked = { fingerprint: endpointFingerprint(this.endpoint), retryAtMs: Date.now() + cooldown };
      }
      this.log(
        'warn',
        'Bridge %s is unreachable after %d attempts; retrying in %d ms while it stays registered.',
        this.endpoint?.pipeName ?? 'endpoint',
        this.attempt,
        cooldown,
      );
      this.setStatus('discovering');
      this.publishManifestCleared();
      return;
    }

    const delay = this.options.reconnectDelayMs * Math.pow(2, Math.min(this.attempt, 8));
    this.attempt += 1;
    this.setStatus('reconnecting');
    this.log('info', 'Reconnect scheduled in %d ms (attempt %d of %d).', delay, this.attempt, this.options.maxReconnectAttempts);
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
        // Close the socket explicitly: a hung bridge answers nothing, and dropping the reference
        // without closing would leak the pipe handle. close() re-enters onClientClosed.
        client.close();
      });
    }, this.options.heartbeatIntervalMs);
  }

  private disconnect(): void {
    this.clearReconnectTimer();
    this.clearHeartbeatTimer();
    const client = this.client;
    this.client = null;
    client?.close();
  }

  /** Tells the adapter to stop advertising tools, exactly once per availability transition. */
  private publishManifestCleared(): void {
    if (!this.manifestPublished) {
      return;
    }
    this.manifestPublished = false;
    this.emit('manifestCleared');
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
