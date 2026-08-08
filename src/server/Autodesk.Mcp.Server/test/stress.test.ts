import { afterEach, describe, expect, it } from 'vitest';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { InMemoryTransport } from '@modelcontextprotocol/sdk/inMemory.js';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { BridgeManager } from '../src/manager.js';
import { McpAdapter } from '../src/mcp/mcpAdapter.js';
import { FakeBridge, uniquePipeName } from './helpers/fakeBridge.js';
import type { Manifest } from '../src/protocol/types.js';

/** Builds a manifest with `count` tools (large-catalog stress fixture). */
function largeManifest(count: number): Manifest {
  return {
    schemaVersion: 1,
    protocolVersion: '1.0.0',
    generatedAtUtc: new Date().toISOString(),
    tools: Array.from({ length: count }, (_, i) => ({
      name: `stress.tool_${i}`,
      displayName: `Stress Tool ${i}`,
      description: `Generated stress tool ${i}.`,
      version: '1.0.0',
      permission: 'ReadOnly',
      risk: 'Low',
      timeoutMilliseconds: 30000,
      supportsProgress: false,
      supportsCancellation: false,
      inputSchema: { type: 'object', properties: { text: { type: 'string' } }, required: ['text'], additionalProperties: false },
    })),
  };
}

describe('stress: large manifests and concurrent load through the real stack', () => {
  let dir: string;
  let bridge: FakeBridge | null = null;
  let manager: BridgeManager | null = null;
  let adapter: McpAdapter | null = null;
  let client: Client | null = null;
  let clientTransport: InMemoryTransport | null = null;

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
    if (dir !== undefined) {
      fs.rmSync(dir, { recursive: true, force: true });
    }
  });

  async function startStack(manifest: Manifest): Promise<void> {
    const pipeName = uniquePipeName('amcp-stress');
    bridge = new FakeBridge({ pipeName, manifest });
    await bridge.start();

    dir = fs.mkdtempSync(path.join(os.tmpdir(), 'amcp-stress-'));
    fs.writeFileSync(
      path.join(dir, 'Civil3D-stress.json'),
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
      requestTimeoutMs: 15000,
      logger: { info: () => undefined, warn: () => undefined, debug: () => undefined },
    });
    adapter = new McpAdapter({
      serverName: 'autodesk-mcp-server',
      serverVersion: '1.0.0',
      getBridge: () => manager!.getBridge(),
      logger: { info: () => undefined, warn: () => undefined, error: () => undefined, debug: () => undefined },
    });
    manager.on('manifest', (m: Parameters<McpAdapter['updateManifest']>[0]) => adapter!.updateManifest(m));
    manager.on('progress', (p: Parameters<McpAdapter['handleBridgeProgress']>[0]) => adapter!.handleBridgeProgress(p));

    const pair = InMemoryTransport.createLinkedPair();
    clientTransport = pair[0];
    await adapter.attach(pair[1]);
    manager.start();

    client = new Client({ name: 'stress-test-client', version: '1.0.0' });
    await client.connect(pair[0]);
    const start = Date.now();
    while (manager.getManifest() === null && Date.now() - start < 10000) {
      await new Promise((resolve) => setTimeout(resolve, 20));
    }
  }

  it('loads and serves a 500-tool manifest with no duplicate registrations', async () => {
    await startStack(largeManifest(500));
    expect(manager!.getManifest()!.tools.length).toBe(500);

    const result = await client!.listTools();
    expect(result.tools.length).toBe(500);
    const names = new Set(result.tools.map((tool) => tool.name));
    expect(names.size).toBe(500); // no duplicates
    expect(result.tools.some((tool) => tool.name === 'stress.tool_499')).toBe(true);
  });

  it('executes 200 concurrent tool calls without crossing responses', async () => {
    await startStack(largeManifest(50));
    const result = await client!.listTools();
    expect(result.tools.length).toBe(50);

    const calls: Array<Promise<void>> = [];
    for (let i = 0; i < 200; i += 1) {
      const name = `stress.tool_${i % 50}`;
      const text = `call-${i}`;
      calls.push(
        client!.callTool({ name, arguments: { text } }).then((res) => {
          expect(res.isError).toBeFalsy();
          const payload = JSON.stringify(res.content);
          expect(payload).toContain(text);
        }),
      );
    }
    await Promise.all(calls);
  });

  it('serves tools/list quickly for a large catalog (no pathological scaling)', async () => {
    await startStack(largeManifest(500));
    const start = Date.now();
    const result = await client!.listTools();
    const elapsed = Date.now() - start;
    expect(result.tools.length).toBe(500);
    // A hard ceiling far above the observed cost; catches pathological regressions only.
    expect(elapsed).toBeLessThan(2000);
  });
});
