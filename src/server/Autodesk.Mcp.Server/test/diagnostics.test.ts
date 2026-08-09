import { afterEach, describe, expect, it } from 'vitest';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { InMemoryTransport } from '@modelcontextprotocol/sdk/inMemory.js';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { BridgeManager } from '../src/manager.js';
import { McpAdapter } from '../src/mcp/mcpAdapter.js';
import { FakeBridge, failEnvelope, okEnvelope, uniquePipeName } from './helpers/fakeBridge.js';

describe('operational diagnostics: log correlation fields', () => {
  // Mirrors pino behaviour: %s-style placeholders are substituted with the bound
  // arguments. Covers %s %d %f %o %O (pino also supports %%, unused here).
  function format(message: string, ...args: unknown[]): string {
    let index = 0;
    return message.replace(/%[sdfoO]/g, () => String(args[index++] ?? ''));
  }

  function capture(level: string, message: string, ...args: unknown[]): void {
    logs.push(`${level} ${format(message, ...args)}`);
  }

  let dir: string;
  let bridge: FakeBridge | null = null;
  let manager: BridgeManager | null = null;
  let adapter: McpAdapter | null = null;
  let client: Client | null = null;
  let clientTransport: InMemoryTransport | null = null;
  const logs: string[] = [];

  afterEach(async () => {
    clientTransport?.close();
    clientTransport = null;
    client = null;
    await adapter?.close().catch(() => undefined);
    adapter = null;
    manager?.stop();
    manager = null;
    await bridge?.stop();
    bridge = null;
    logs.length = 0;
    if (dir !== undefined) {
      fs.rmSync(dir, { recursive: true, force: true });
    }
  });

  async function startStack(onExecute?: (tool: string, args: unknown, confirm?: boolean) => ReturnType<typeof okEnvelope>): Promise<void> {
    const pipeName = uniquePipeName('amcp-diag');
    bridge = new FakeBridge({ pipeName, onExecute });
    await bridge.start();

    dir = fs.mkdtempSync(path.join(os.tmpdir(), 'amcp-diag-'));
    fs.writeFileSync(
      path.join(dir, 'Civil3D-diag.json'),
      JSON.stringify({
        bridgeName: 'Civil3D.Bridge',
        product: 'Civil3D',
        protocolVersion: '1.0.0',
        bridgeVersion: '1.0.0',
        sdkVersion: '1.0.0',
        pipeName,
        pid: process.pid,
        startedUtc: new Date().toISOString(),
      }),
    );

    manager = new BridgeManager({
      endpointsDir: dir,
      clientName: 'Autodesk.MCP.Server',
      endpointsPollIntervalMs: 50,
      reconnectDelayMs: 50,
      maxReconnectAttempts: 5,
      heartbeatIntervalMs: 0,
      requestTimeoutMs: 5000,
      logger: {
        info: (message: string, ...args: unknown[]) => capture('info', message, ...args),
        warn: (message: string, ...args: unknown[]) => capture('warn', message, ...args),
        debug: (message: string, ...args: unknown[]) => capture('debug', message, ...args),
      },
    });
    adapter = new McpAdapter({
      serverName: 'autodesk-mcp-server',
      serverVersion: '1.0.0',
      getBridge: () => manager!.getBridge(),
      logger: {
        info: (message: string, ...args: unknown[]) => capture('info', message, ...args),
        warn: (message: string, ...args: unknown[]) => capture('warn', message, ...args),
        error: (message: string, ...args: unknown[]) => capture('error', message, ...args),
        debug: (message: string, ...args: unknown[]) => capture('debug', message, ...args),
      },
    });
    manager.on('manifest', (manifest: Parameters<McpAdapter['updateManifest']>[0]) => adapter!.updateManifest(manifest));
    manager.on('progress', (progress: Parameters<McpAdapter['handleBridgeProgress']>[0]) => adapter!.handleBridgeProgress(progress));

    const pair = InMemoryTransport.createLinkedPair();
    clientTransport = pair[0];
    await adapter.attach(pair[1]);
    manager.start();

    client = new Client({ name: 'diag-test-client', version: '1.0.0' });
    await client.connect(pair[0]);
    const start = Date.now();
    while (manager.getManifest() === null && Date.now() - start < 5000) {
      await new Promise((resolve) => setTimeout(resolve, 20));
    }
  }

  it('logs tool execution with the tool name and correlation id', async () => {
    await startStack(() => okEnvelope({ ok: true }));
    const result = await client!.callTool({ name: 'echo', arguments: { text: 'x' } });
    expect(result.isError).toBeFalsy();

    const joined = logs.join('\n');
    expect(joined).toContain('Tool echo');
    expect(joined).toMatch(/correlation [0-9a-f-]{36}/);
    expect(joined).toContain('succeeded');
  });

  it('logs failures with the stable error code and correlation id', async () => {
    await startStack(() => failEnvelope('E_NO_ACTIVE_DOCUMENT', 'Open a drawing first.'));
    const result = await client!.callTool({ name: 'echo', arguments: { text: 'x' } });
    expect(result.isError).toBe(true);

    const joined = logs.join('\n');
    expect(joined).toContain('E_NO_ACTIVE_DOCUMENT');
    expect(joined).toContain('Open a drawing first.');
    expect(joined).toMatch(/correlation [0-9a-f-]{36}/);
  });

  it('logs bridge selection with product, bridge name and pipe (multi-instance diagnostics)', async () => {
    await startStack();
    // The manager logs the selected endpoint; assert the fields an operator needs appear.
    const joined = logs.join('\n');
    expect(joined).toContain('Endpoint discovered');
    expect(joined).toContain('Civil3D.Bridge');
    expect(joined).toContain('Civil3D');
    expect(joined).toContain(bridge!.pipeName);
  });

  it('logs the whole startup lifecycle so an empty tool list is explainable', async () => {
    await startStack();
    const joined = logs.join('\n');
    // Each transition an operator needs to diagnose "Discovered 0 tools".
    expect(joined).toContain('Searching for bridge endpoints');
    expect(joined).toContain('Endpoint discovered');
    expect(joined).toContain('Connecting to bridge on pipe');
    expect(joined).toContain('Handshake succeeded');
    expect(joined).toContain('Manifest loaded');
    expect(joined).toMatch(/\d+ tool\(s\) available/);
  });

  it('logs reconnection after a bridge loss with the pipe name', async () => {
    await startStack();
    bridge!.abortAllConnections();
    await new Promise((resolve) => setTimeout(resolve, 300));
    const joined = logs.join('\n');
    expect(joined).toContain('reconnect');
    expect(joined).toContain('disconnected');
    expect(joined).toContain(bridge!.pipeName);
  });
});
