import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { InMemoryTransport } from '@modelcontextprotocol/sdk/inMemory.js';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { ProgressNotificationSchema } from '@modelcontextprotocol/sdk/types.js';
import { BridgeManager } from '../src/manager.js';
import { McpAdapter } from '../src/mcp/mcpAdapter.js';
import type { ResponseEnvelope } from '../src/protocol/types.js';
import { FakeBridge, failEnvelope, okEnvelope, uniquePipeName } from './helpers/fakeBridge.js';

describe('MCP integration (discovery to protocol response)', () => {
  let dir: string;
  let bridge: FakeBridge | null = null;
  let manager: BridgeManager | null = null;
  let adapter: McpAdapter | null = null;
  let client: Client | null = null;
  let clientTransport: InMemoryTransport | null = null;

  beforeEach(() => {
    dir = fs.mkdtempSync(path.join(os.tmpdir(), 'amcp-mcp-'));
  });

  afterEach(async () => {
    clientTransport?.close();
    clientTransport = null;
    client = null;
    await adapter?.close();
    adapter = null;
    manager?.stop();
    manager = null;
    await bridge?.stop();
    bridge = null;
    fs.rmSync(dir, { recursive: true, force: true });
  });

  async function startStack(options?: {
    onExecute?: (tool: string, args: unknown, confirm?: boolean) => ResponseEnvelope | Promise<ResponseEnvelope>;
    executeDelayMs?: number;
  }): Promise<{ pipeName: string }> {
    const pipeName = uniquePipeName();
    bridge = new FakeBridge({ pipeName, ...options });
    await bridge.start();
    fs.writeFileSync(
      path.join(dir, 'Civil3D-test.json'),
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
      maxReconnectAttempts: 3,
      heartbeatIntervalMs: 0,
      requestTimeoutMs: 5000,
    });
    adapter = new McpAdapter({
      serverName: 'autodesk-mcp-server',
      serverVersion: '1.0.0',
      getBridge: () => manager!.getBridge(),
      logger: {
        info: () => undefined,
        warn: () => undefined,
        error: () => undefined,
        debug: () => undefined,
      },
    });

    manager.on('manifest', (manifest: Parameters<McpAdapter['updateManifest']>[0]) => adapter!.updateManifest(manifest));
    manager.on('manifestCleared', () => adapter!.clearManifest());
    manager.on('progress', (progress: Parameters<McpAdapter['handleBridgeProgress']>[0]) => adapter!.handleBridgeProgress(progress));

    const pair = InMemoryTransport.createLinkedPair();
    clientTransport = pair[0];
    await adapter.attach(pair[1]);
    manager.start();

    client = new Client({ name: 'test-mcp-client', version: '1.0.0' });
    await client.connect(pair[0]);

    // Wait for the manager to connect and load the manifest.
    const start = Date.now();
    while (manager.getManifest() === null && Date.now() - start < 5000) {
      await new Promise((resolve) => setTimeout(resolve, 20));
    }
    return { pipeName };
  }

  async function toolNames(): Promise<string[]> {
    const result = await client!.listTools();
    return result.tools.map((tool) => tool.name);
  }

  /** Extracts the text of the first text content block of a callTool result. */
  function firstText(result: unknown): string {
    const content = (result as { content?: unknown }).content as Array<{ type?: string; text?: string }> | undefined;
    const block = content?.[0];
    return block?.type === 'text' ? (block.text ?? '') : '';
  }

  it('discovers the bridge and exposes its manifest as MCP tools', async () => {
    await startStack();
    const names = await toolNames();
    expect(names).toContain('drawing_info');
    expect(names).toContain('echo');
    expect(names).toContain('rename_alignment');
  });

  it('exposes the bridge input schema and annotations on each tool', async () => {
    await startStack();
    const result = await client!.listTools();
    const echo = result.tools.find((tool) => tool.name === 'echo')!;
    expect(echo.inputSchema).toMatchObject({ required: ['text'] });
    expect(echo.annotations?.title).toBe('Echo');
    expect(echo.annotations?.readOnlyHint).toBe(true);
  });

  it('executes a tool end to end and returns the bridge payload as text content', async () => {
    await startStack();
    const result = await client!.callTool({ name: 'echo', arguments: { text: 'hello' } });
    expect(result.isError).toBeFalsy();
    expect(JSON.parse(firstText(result))).toMatchObject({ tool: 'echo', echoed: { text: 'hello' } });
  });

  it('rejects invalid arguments against the bridge schema with an invalid-params error', async () => {
    await startStack();
    await expect(
      client!.callTool({ name: 'echo', arguments: {} }),
    ).rejects.toMatchObject({ code: -32602 });
  });

  it('maps bridge business failures to structured isError results with the code preserved', async () => {
    await startStack({
      onExecute: () => failEnvelope('E_NO_ACTIVE_DOCUMENT', 'Open a drawing first.'),
    });
    const result = await client!.callTool({ name: 'drawing_info', arguments: {} });
    expect(result.isError).toBe(true);
    const parsed = JSON.parse(firstText(result));
    expect(parsed.code).toBe('E_NO_ACTIVE_DOCUMENT');
    expect(parsed.message).toBe('Open a drawing first.');
  });

  it('returns confirmation-required with retry guidance, then succeeds with confirm: true', async () => {
    await startStack({
      onExecute: (tool, args, confirm) =>
        confirm === true
          ? okEnvelope({ renamed: true })
          : failEnvelope('E_CONFIRMATION_REQUIRED', 'Confirm the rename before proceeding.'),
    });

    const first = await client!.callTool({ name: 'rename_alignment', arguments: { id: 1, newName: 'Road A' } });
    expect(first.isError).toBe(true);
    const parsed = JSON.parse(firstText(first));
    expect(parsed.code).toBe('E_CONFIRMATION_REQUIRED');
    expect(parsed.confirmation.retryWith).toEqual({ confirm: true });

    const second = await client!.callTool({
      name: 'rename_alignment',
      arguments: { id: 1, newName: 'Road A', confirm: true },
    });
    expect(second.isError).toBeFalsy();
    expect(JSON.parse(firstText(second))).toMatchObject({ renamed: true });
  });

  it('maps unknown tools to a structured object-not-found result', async () => {
    await startStack({
      onExecute: (tool) => failEnvelope('E_OBJECT_NOT_FOUND', `Unknown tool '${tool}'.`),
    });
    const result = await client!.callTool({ name: 'nope', arguments: {} });
    expect(result.isError).toBe(true);
    expect(JSON.parse(firstText(result))).toMatchObject({
      code: 'E_OBJECT_NOT_FOUND',
    });
  });

  it('forwards bridge progress to the MCP client using the supplied progress token', async () => {
    await startStack({
      onExecute: () => new Promise((resolve) => setTimeout(() => resolve(okEnvelope({ done: true })), 300)),
    });
    const progressEvents: Array<{ progress: number; message?: string }> = [];
    client!.setNotificationHandler(ProgressNotificationSchema, (notification) => {
      const params = notification.params;
      progressEvents.push({ progress: params.progress ?? 0, message: params.message });
    });

    const pending = client!.callTool({
      name: 'echo',
      arguments: { text: 'slow', _meta: { progressToken: 'tok-42' } },
    });
    // The bridge streams progress for the in-flight correlation (first execute request).
    await new Promise((resolve) => setTimeout(resolve, 100));
    const executeRequest = bridge!.requests.find((request) => request.method === 'tools/execute');
    const correlationId = executeRequest?.correlationId;
    expect(correlationId).toBeDefined();

    bridge!.sendProgress(correlationId!, 25, 'working', 'step 1');
    const result = await pending;
    expect(result.isError).toBeFalsy();
    expect(JSON.parse(firstText(result))).toMatchObject({ done: true });

    // The progress notification is forwarded synchronously over the emitter chain; the extra
    // wait only guards against scheduler jitter.
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(progressEvents).toHaveLength(1);
    expect(progressEvents[0]?.progress).toBe(25);
    expect(progressEvents[0]?.message).toContain('working');
  });

  it('forwards client cancellation as a bridge $/cancel notification', async () => {
    await startStack({ executeDelayMs: 3000 });

    // Capture the JSON-RPC id of the outgoing tools/call so the client can cancel it.
    const sent: Array<{ method?: string; id?: number | string }> = [];
    const transport = clientTransport!;
    const originalSend = transport.send.bind(transport);
    transport.send = (async (message: { method?: string; id?: number | string }) => {
      sent.push(message);
      await originalSend(message as never);
    }) as typeof transport.send;

    const pending = client!.callTool({ name: 'echo', arguments: { text: 'slow' } });
    pending.catch(() => undefined); // the abort surfaces as an MCP error; do not fail the test
    await new Promise((resolve) => setTimeout(resolve, 100));
    const call = sent.find((message) => message.method === 'tools/call');
    expect(call?.id).toBeDefined();

    await client!.notification({
      method: 'notifications/cancelled',
      params: { requestId: call!.id!, reason: 'integration test cancellation' },
    });
    await new Promise((resolve) => setTimeout(resolve, 200));

    const executeRequest = bridge!.requests.find((request) => request.method === 'tools/execute');
    expect(bridge!.cancels).toContain(executeRequest?.correlationId);
  });

  it('reconnects when the bridge restarts and the new manifest replaces the old one', async () => {
    await startStack();
    expect(await toolNames()).toContain('echo');

    await bridge!.stop();
    fs.rmSync(path.join(dir, 'Civil3D-test.json'));
    await new Promise((resolve) => setTimeout(resolve, 200));

    // Restart the bridge on a fresh pipe and re-publish its endpoint.
    bridge = new FakeBridge({ pipeName: uniquePipeName() });
    await bridge.start();
    fs.writeFileSync(
      path.join(dir, 'Civil3D-test.json'),
      JSON.stringify({
        bridgeName: 'Civil3D.Bridge',
        product: 'Civil3D',
        protocolVersion: '1.0.0',
        bridgeVersion: '1.0.0',
        sdkVersion: '1.0.0',
        pipeName: bridge.pipeName,
        pid: process.pid,
        startedUtc: new Date().toISOString(),
      }),
    );

    const start = Date.now();
    while ((manager!.getManifest()?.tools.length ?? 0) === 0 && Date.now() - start < 5000) {
      await new Promise((resolve) => setTimeout(resolve, 20));
    }
    expect(manager!.getManifest()?.tools.length).toBeGreaterThan(0);
    expect(await toolNames()).toContain('echo');
  });
});
