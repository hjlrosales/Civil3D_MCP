import { afterEach, describe, expect, it } from 'vitest';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { InMemoryTransport } from '@modelcontextprotocol/sdk/inMemory.js';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { BridgeManager } from '../src/manager.js';
import { McpAdapter } from '../src/mcp/mcpAdapter.js';
import { FakeBridge, okEnvelope, uniquePipeName } from './helpers/fakeBridge.js';

describe('concurrency: cancellation isolation', () => {
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
    const pipeName = uniquePipeName('amcp-cancel-isolation');
    bridge = new FakeBridge({
      pipeName,
      onExecute: async (tool, args) => {
        const text = (args as { text?: string } | undefined)?.text ?? '';
        if (text === 'slow-a') {
          await new Promise((resolve) => setTimeout(resolve, 800));
        }
        return okEnvelope({ done: true });
      },
    });
    await bridge.start();

    dir = fs.mkdtempSync(path.join(os.tmpdir(), 'amcp-cancel-isolation-'));
    fs.writeFileSync(
      path.join(dir, 'Civil3D-cancel.json'),
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
      requestTimeoutMs: 10000,
      logger: { info: () => undefined, warn: () => undefined, debug: () => undefined },
    });
    manager.start();
    const start = Date.now();
    while (manager.getManifest() === null && Date.now() - start < 5000) {
      await new Promise((resolve) => setTimeout(resolve, 20));
    }
  }

  async function addClient(name: string): Promise<{ client: Client; adapter: McpAdapter }> {
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

    const client = new Client({ name, version: '1.0.0' });
    await client.connect(pair[0]);
    clients.push({ client, transport: pair[0] });
    return { client, adapter };
  }

  function firstText(result: unknown): string {
    const content = (result as { content?: Array<{ type?: string; text?: string }> }).content;
    const block = content?.[0];
    return block !== undefined && block.type === 'text' ? (block.text ?? '') : '';
  }

  it('cancelling one correlation id does not affect other in-flight requests', async () => {
    await startStack();
    const { client: clientA, adapter: adapterA } = await addClient('client-a');
    const { client: clientB } = await addClient('client-b');

    // Capture the JSON-RPC id of A's outgoing tools/call so the client can cancel exactly it.
    const sentA: Array<{ method?: string; id?: number | string }> = [];
    const transportA = clients[0]!.transport;
    const originalSendA = transportA.send.bind(transportA);
    transportA.send = (async (message: { method?: string; id?: number | string }) => {
      sentA.push(message);
      await originalSendA(message as never);
    }) as typeof transportA.send;

    const pendingA = clientA.callTool({ name: 'echo', arguments: { text: 'slow-a' } });
    pendingA.catch(() => undefined);
    await new Promise((resolve) => setTimeout(resolve, 150));

    // B's call starts while A is in flight and completes normally (unaffected by A's cancel).
    const fastResult = await clientB.callTool({ name: 'echo', arguments: { text: 'fast-b' } });
    expect(fastResult.isError).toBeFalsy();
    expect(JSON.parse(firstText(fastResult))).toMatchObject({ done: true });

    // Cancel A's request.
    const callA = sentA.find((message) => message.method === 'tools/call');
    expect(callA?.id).toBeDefined();
    await clientA.notification({
      method: 'notifications/cancelled',
      params: { requestId: callA!.id!, reason: 'isolation test' },
    });
    await new Promise((resolve) => setTimeout(resolve, 200));

    // The bridge received exactly one $/cancel, for A's correlation only.
    expect(bridge!.cancels).toHaveLength(1);
    const aCorrelation = bridge!.requests.filter((r) => r.method === 'tools/execute')[0]?.correlationId;
    expect(bridge!.cancels[0]).toBe(aCorrelation);
    void adapterA;
  });
});
