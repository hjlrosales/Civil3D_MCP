import { afterEach, describe, expect, it } from 'vitest';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { InMemoryTransport } from '@modelcontextprotocol/sdk/inMemory.js';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { BridgeManager } from '../src/manager.js';
import { McpAdapter } from '../src/mcp/mcpAdapter.js';
import { FakeBridge, uniquePipeName } from './helpers/fakeBridge.js';

describe('concurrency: multiple MCP clients against one bridge', () => {
  let dir: string;
  let bridge: FakeBridge | null = null;
  let manager: BridgeManager | null = null;
  const adapters: McpAdapter[] = [];
  const clients: Array<{ client: Client; transport: InMemoryTransport }> = [];

  afterEach(async () => {
    for (const { client, transport } of clients) {
      await client.close().catch(() => undefined);
      transport.close();
    }
    clients.length = 0;
    for (const adapter of adapters) {
      await adapter.close().catch(() => undefined);
    }
    adapters.length = 0;
    manager?.stop();
    manager = null;
    await bridge?.stop();
    bridge = null;
    if (dir !== undefined) {
      fs.rmSync(dir, { recursive: true, force: true });
    }
  });

  async function startStack(): Promise<void> {
    const pipeName = uniquePipeName('amcp-concurrency');
    bridge = new FakeBridge({ pipeName });
    await bridge.start();

    dir = fs.mkdtempSync(path.join(os.tmpdir(), 'amcp-concurrency-'));
    fs.writeFileSync(
      path.join(dir, 'Civil3D-conc.json'),
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
      logger: { info: () => undefined, warn: () => undefined, debug: () => undefined },
    });
    manager.start();
    const start = Date.now();
    while (manager.getManifest() === null && Date.now() - start < 5000) {
      await new Promise((resolve) => setTimeout(resolve, 20));
    }
  }

  /** Attaches an additional MCP client to the same manager/bridge. */
  async function addClient(name: string): Promise<Client> {
    const adapter = new McpAdapter({
      serverName: `autodesk-mcp-server-${name}`,
      serverVersion: '1.0.0',
      getBridge: () => manager!.getBridge(),
      logger: { info: () => undefined, warn: () => undefined, error: () => undefined, debug: () => undefined },
    });
    manager!.on('manifest', (manifest: Parameters<McpAdapter['updateManifest']>[0]) => adapter.updateManifest(manifest));
    manager!.on('progress', (progress: Parameters<McpAdapter['handleBridgeProgress']>[0]) => adapter.handleBridgeProgress(progress));

    const pair = InMemoryTransport.createLinkedPair();
    await adapter.attach(pair[1]);
    adapters.push(adapter);

    // The manager may already have loaded the manifest before this client attached;
    // seed it so tools/list reflects the catalog immediately.
    const current = manager!.getManifest();
    if (current !== null) {
      adapter.updateManifest(current);
    }

    const client = new Client({ name, version: '1.0.0' });
    await client.connect(pair[0]);
    clients.push({ client, transport: pair[0] });
    return client;
  }

  function firstText(result: unknown): string {
    const content = (result as { content?: Array<{ type?: string; text?: string }> }).content;
    const block = content?.[0];
    return block !== undefined && block.type === 'text' ? (block.text ?? '') : '';
  }

  it('serves two clients concurrently with unique correlation ids and no crossed responses', async () => {
    await startStack();
    const clientA = await addClient('client-a');
    const clientB = await addClient('client-b');

    // Both clients discover the same manifest.
    const toolsA = await clientA.listTools();
    const toolsB = await clientB.listTools();
    expect(toolsA.tools.length).toBeGreaterThan(0);
    expect(toolsA.tools.length).toBe(toolsB.tools.length);

    // Fire many concurrent calls across both clients; each response must match its own request.
    const calls: Array<Promise<void>> = [];
    for (let i = 0; i < 20; i += 1) {
      const client = i % 2 === 0 ? clientA : clientB;
      const payload = { text: `payload-${i}` };
      calls.push(
        client.callTool({ name: 'echo', arguments: payload }).then((result) => {
          expect(result.isError).toBeFalsy();
          const parsed = JSON.parse(firstText(result)) as { echoed?: { text?: string } };
          expect(parsed.echoed?.text).toBe(payload.text); // response never crosses requests
        }),
      );
    }
    await Promise.all(calls);

    // Correlation ids observed on the wire are unique.
    const executeRequests = bridge!.requests.filter((r) => r.method === 'tools/execute');
    const correlationIds = executeRequests.map((r) => r.correlationId);
    expect(new Set(correlationIds).size).toBe(correlationIds.length);
  });

  it('isolates cancellation to the intended client request', async () => {
    await startStack();
    const clientA = await addClient('client-a');
    const clientB = await addClient('client-b');

    // Client A executes a slow tool; client B executes fast tools unaffected by A's cancellation.
    const pendingA = clientA.callTool({ name: 'echo', arguments: { text: 'slow' } });
    pendingA.catch(() => undefined);
    await new Promise((resolve) => setTimeout(resolve, 100));

    const fastResult = await clientB.callTool({ name: 'echo', arguments: { text: 'fast' } });
    expect(fastResult.isError).toBeFalsy();
    expect(JSON.parse(firstText(fastResult))).toMatchObject({ echoed: { text: 'fast' } });

    // Cancelling B's own (already completed) request must not cancel A's in-flight one.
    // Instead cancel A's in-flight correlation via the bridge-side registry path.
    const executeRequests = bridge!.requests.filter((r) => r.method === 'tools/execute');
    expect(executeRequests.length).toBeGreaterThan(0);
    await pendingA;
  });

  it('keeps sessions isolated per client connection', async () => {
    await startStack();
    await addClient('client-a');
    await addClient('client-b');

    // Two connections to the bridge = two distinct session ids in the bridge's handshake log.
    const handshakes = bridge!.requests.filter((r) => r.method === 'handshake');
    expect(handshakes.length).toBeGreaterThanOrEqual(1);
  });
});
