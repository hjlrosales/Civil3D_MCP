import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import type { EventEmitter } from 'node:events';
import { InMemoryTransport } from '@modelcontextprotocol/sdk/inMemory.js';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { ToolListChangedNotificationSchema } from '@modelcontextprotocol/sdk/types.js';
import { BridgeManager } from '../../src/manager.js';
import { McpAdapter } from '../../src/mcp/mcpAdapter.js';
import { FakeBridge, uniquePipeName } from './fakeBridge.js';
import type { Manifest } from '../../src/protocol/types.js';

/**
 * Lifecycle-test harness: drives a real BridgeManager + McpAdapter + MCP SDK client over an
 * in-memory transport against a real named-pipe FakeBridge, with an on-disk endpoint registry.
 * The whole stack is exercised exactly as index.ts wires it, so lifecycle regressions (restart,
 * reconnect, endpoint churn) are reproduced end to end rather than mocked.
 */

/** Resolves once `predicate` holds, or rejects after `timeoutMs`. */
export async function waitUntil(predicate: () => boolean, timeoutMs = 8_000, label = 'condition'): Promise<void> {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if (predicate()) {
      return;
    }
    await new Promise((resolve) => setTimeout(resolve, 10));
  }
  throw new Error(`Timed out after ${timeoutMs} ms waiting for ${label}.`);
}

/**
 * Resolves the next time `emitter` fires `event`. Registered before the triggering action so the
 * wait is deterministic rather than a sleep.
 */
export function waitForEvent<T = unknown>(emitter: EventEmitter, event: string, timeoutMs = 8_000): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const timer = setTimeout(() => {
      emitter.off(event, onEvent);
      reject(new Error(`Timed out after ${timeoutMs} ms waiting for '${event}'.`));
    }, timeoutMs);
    const onEvent = (payload: T): void => {
      clearTimeout(timer);
      resolve(payload);
    };
    emitter.once(event, onEvent);
  });
}

export interface EndpointFields {
  pipeName: string;
  pid?: number;
  bridgeName?: string;
  product?: string;
  startedUtc?: string;
}

/** A running server-side stack (manager + adapter + connected MCP client). */
export class LifecycleHarness {
  readonly endpointsDir: string;
  private readonly bridges = new Set<FakeBridge>();
  private manager: BridgeManager | null = null;
  private adapter: McpAdapter | null = null;
  private client: Client | null = null;
  private transports: InMemoryTransport[] = [];

  /** Every notifications/tools/list_changed received by the MCP client. */
  readonly toolListChanges: number[] = [];

  constructor() {
    this.endpointsDir = fs.mkdtempSync(path.join(os.tmpdir(), 'amcp-lifecycle-'));
  }

  get bridgeManager(): BridgeManager {
    if (this.manager === null) {
      throw new Error('The server stack is not running.');
    }
    return this.manager;
  }

  get mcpClient(): Client {
    if (this.client === null) {
      throw new Error('The MCP client is not connected.');
    }
    return this.client;
  }

  get mcpAdapter(): McpAdapter {
    if (this.adapter === null) {
      throw new Error('The MCP adapter is not attached.');
    }
    return this.adapter;
  }

  /** Starts a FakeBridge on a fresh pipe (does not publish an endpoint descriptor). */
  async startBridge(pipeName = uniquePipeName(), manifest?: Manifest): Promise<FakeBridge> {
    const bridge = new FakeBridge({ pipeName, manifest });
    this.bridges.add(bridge);
    await bridge.start();
    return bridge;
  }

  /** Stops a FakeBridge and forgets it. */
  async stopBridge(bridge: FakeBridge): Promise<void> {
    this.bridges.delete(bridge);
    await bridge.stop();
  }

  /** Writes an endpoint descriptor file, mimicking BridgeHost endpoint registration. */
  writeEndpoint(fileName: string, fields: EndpointFields): void {
    fs.writeFileSync(
      path.join(this.endpointsDir, fileName),
      JSON.stringify({
        bridgeName: fields.bridgeName ?? 'Civil3D.Bridge',
        product: fields.product ?? 'Civil3D',
        productVersion: '2025',
        protocolVersion: '1.0.0',
        bridgeVersion: '1.0.0',
        sdkVersion: '1.0.0',
        pipeName: fields.pipeName,
        pid: fields.pid ?? process.pid,
        startedUtc: fields.startedUtc ?? new Date().toISOString(),
        lastHeartbeatAtUtc: new Date().toISOString(),
      }),
    );
  }

  /** Removes an endpoint descriptor file, mimicking bridge shutdown / crash cleanup. */
  removeEndpoint(fileName: string): void {
    fs.rmSync(path.join(this.endpointsDir, fileName), { force: true });
  }

  /** Starts the MCP server stack (manager + adapter) and connects an MCP client to it. */
  async startServer(options?: {
    maxReconnectAttempts?: number;
    reconnectDelayMs?: number;
    endpointsPollIntervalMs?: number;
    heartbeatIntervalMs?: number;
    retryCooldownMs?: number;
  }): Promise<void> {
    if (this.manager !== null) {
      throw new Error('The server stack is already running.');
    }

    this.manager = new BridgeManager({
      endpointsDir: this.endpointsDir,
      clientName: 'Autodesk.MCP.Server',
      endpointsPollIntervalMs: options?.endpointsPollIntervalMs ?? 25,
      reconnectDelayMs: options?.reconnectDelayMs ?? 20,
      maxReconnectAttempts: options?.maxReconnectAttempts ?? 20,
      retryCooldownMs: options?.retryCooldownMs ?? 100,
      heartbeatIntervalMs: options?.heartbeatIntervalMs ?? 0,
      requestTimeoutMs: 5_000,
      logger: { info: () => undefined, warn: () => undefined, debug: () => undefined },
    });

    const manager = this.manager;
    this.adapter = new McpAdapter({
      serverName: 'autodesk-mcp-server',
      serverVersion: '1.0.0',
      getBridge: () => manager.getBridge(),
      logger: { info: () => undefined, warn: () => undefined, error: () => undefined, debug: () => undefined },
    });
    const adapter = this.adapter;

    manager.on('manifest', (manifest: Manifest) => adapter.updateManifest(manifest));
    manager.on('manifestCleared', () => adapter.clearManifest());

    const [clientTransport, serverTransport] = InMemoryTransport.createLinkedPair();
    this.transports = [clientTransport, serverTransport];
    await adapter.attach(serverTransport);

    this.client = new Client({ name: 'lifecycle-test-client', version: '1.0.0' });
    this.client.setNotificationHandler(ToolListChangedNotificationSchema, () => {
      this.toolListChanges.push(Date.now());
    });
    await this.client.connect(clientTransport);

    // The manager only starts after the transport is live, exactly as index.ts does it.
    manager.start();
  }

  /** Stops the MCP server stack, leaving bridges and the endpoint registry untouched. */
  async stopServer(): Promise<void> {
    await this.client?.close();
    this.client = null;
    await this.adapter?.close();
    this.adapter = null;
    this.manager?.stop();
    this.manager = null;
    for (const transport of this.transports) {
      await transport.close();
    }
    this.transports = [];
    this.toolListChanges.length = 0;
  }

  /** Current MCP tool names as the client sees them. */
  async toolNames(): Promise<string[]> {
    const result = await this.mcpClient.listTools();
    return result.tools.map((tool) => tool.name).sort();
  }

  /** Polls tools/list until it reports exactly this set of tool names. */
  async waitForToolNames(expected: string[], timeoutMs = 8_000): Promise<void> {
    const target = [...expected].sort().join(',');
    const start = Date.now();
    let observed = '';
    while (Date.now() - start < timeoutMs) {
      observed = (await this.toolNames()).join(',');
      if (observed === target) {
        return;
      }
      await new Promise((resolve) => setTimeout(resolve, 10));
    }
    throw new Error(`Timed out waiting for tools/list to report [${target}] (last saw [${observed}]).`);
  }

  /** Polls tools/list until it reports exactly `count` tools. */
  async waitForToolCount(count: number, timeoutMs = 8_000): Promise<void> {
    const start = Date.now();
    let observed = -1;
    while (Date.now() - start < timeoutMs) {
      observed = (await this.mcpClient.listTools()).tools.length;
      if (observed === count) {
        return;
      }
      await new Promise((resolve) => setTimeout(resolve, 10));
    }
    throw new Error(`Timed out waiting for tools/list to report ${count} tools (last saw ${observed}).`);
  }

  /** Tears down everything: client, adapter, manager, bridges and the endpoint registry. */
  async dispose(): Promise<void> {
    await this.stopServer();
    for (const bridge of this.bridges) {
      await bridge.stop();
    }
    this.bridges.clear();
    fs.rmSync(this.endpointsDir, { recursive: true, force: true });
  }
}
