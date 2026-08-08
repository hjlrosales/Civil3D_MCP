import { afterEach, describe, expect, it } from 'vitest';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { InMemoryTransport } from '@modelcontextprotocol/sdk/inMemory.js';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { BridgeManager } from '../src/manager.js';
import { McpAdapter } from '../src/mcp/mcpAdapter.js';
import { FakeBridge, failEnvelope, okEnvelope, uniquePipeName } from './helpers/fakeBridge.js';
import { NdjsonSocket } from '../src/transport/ndjson.js';
import { MaxMessageLength } from '../src/protocol/constants.js';
import { PassThrough } from 'node:stream';

describe('security: payload limits and exception containment', () => {
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

  async function startStack(onExecute?: (tool: string, args: unknown, confirm?: boolean) => ReturnType<typeof okEnvelope>): Promise<void> {
    const pipeName = uniquePipeName('amcp-security');
    bridge = new FakeBridge({ pipeName, onExecute });
    await bridge.start();

    dir = fs.mkdtempSync(path.join(os.tmpdir(), 'amcp-security-'));
    fs.writeFileSync(
      path.join(dir, 'Civil3D-sec.json'),
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
    adapter = new McpAdapter({
      serverName: 'autodesk-mcp-server',
      serverVersion: '1.0.0',
      getBridge: () => manager!.getBridge(),
      logger: { info: () => undefined, warn: () => undefined, error: () => undefined, debug: () => undefined },
    });
    manager.on('manifest', (manifest: Parameters<McpAdapter['updateManifest']>[0]) => adapter!.updateManifest(manifest));
    manager.on('progress', (progress: Parameters<McpAdapter['handleBridgeProgress']>[0]) => adapter!.handleBridgeProgress(progress));

    const pair = InMemoryTransport.createLinkedPair();
    clientTransport = pair[0];
    await adapter.attach(pair[1]);
    manager.start();

    client = new Client({ name: 'security-test-client', version: '1.0.0' });
    await client.connect(pair[0]);
    const start = Date.now();
    while (manager.getManifest() === null && Date.now() - start < 5000) {
      await new Promise((resolve) => setTimeout(resolve, 20));
    }
  }

  function firstText(result: unknown): string {
    const content = (result as { content?: Array<{ type?: string; text?: string }> }).content;
    const block = content?.[0];
    return block !== undefined && block.type === 'text' ? (block.text ?? '') : '';
  }

  it('never leaks internal exception details or stack traces across the protocol boundary', async () => {
    // The bridge returns a generic E_INTERNAL; raw exceptions must not cross.
    await startStack(() => failEnvelope('E_INTERNAL', 'An internal error occurred while executing the tool.'));
    const result = await client!.callTool({ name: 'echo', arguments: { text: 'x' } });
    expect(result.isError).toBe(true);
    const text = firstText(result);
    expect(text).toContain('E_INTERNAL');
    expect(text).not.toContain('Stack trace');
    expect(text).not.toContain('at Autodesk');
    expect(text).not.toMatch(/\n\s+at /);
  });

  it('rejects oversized NDJSON wire messages (DoS guard)', async () => {
    const duplex = new PassThrough();
    const ndjson = new NdjsonSocket(duplex);
    const errors: Error[] = [];
    ndjson.on('error', (error: Error) => errors.push(error));

    // A single line larger than the hard cap must terminate the connection.
    duplex.write('x'.repeat(MaxMessageLength + 1));
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(errors.length).toBeGreaterThan(0);
    expect(errors[0]!.message).toContain('maximum allowed length');
  });

  it('rejects malformed tool arguments with invalid-params (no crash, no leak)', async () => {
    await startStack();
    await expect(
      client!.callTool({ name: 'echo', arguments: { wrong: 42 } }),
    ).rejects.toMatchObject({ code: -32602 });

    // The server stays healthy and serves the next request.
    const ok = await client!.callTool({ name: 'echo', arguments: { text: 'still alive' } });
    expect(ok.isError).toBeFalsy();
  });

  it('treats non-object tool arguments as empty and rejects against the schema', async () => {
    await startStack();
    await expect(client!.callTool({ name: 'echo', arguments: 'not-an-object' as never })).rejects.toThrow();
  });

  it('maps unknown tools to a structured result, never a raw error', async () => {
    await startStack((tool) => failEnvelope('E_OBJECT_NOT_FOUND', `Unknown tool '${tool}'.`));
    const result = await client!.callTool({ name: 'nope', arguments: {} });
    expect(result.isError).toBe(true);
    const parsed = JSON.parse(firstText(result)) as { code?: string };
    expect(parsed.code).toBe('E_OBJECT_NOT_FOUND');
  });
});
