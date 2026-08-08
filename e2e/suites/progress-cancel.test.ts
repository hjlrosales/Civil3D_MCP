import { afterEach, describe, expect, it } from 'vitest';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { FakeBridge, okEnvelope, uniquePipeName } from '../../src/server/Autodesk.Mcp.Server/test/helpers/fakeBridge.js';
import { RawMcpClient } from '../helpers/rawClient.js';
import { distIndex, writeDescriptor } from '../helpers/harness.js';

describe('progress and cancellation end to end', () => {
  let bridge: FakeBridge | null = null;
  let client: RawMcpClient | null = null;
  let endpointsDir: string | null = null;

  afterEach(async () => {
    client?.close();
    client = null;
    await bridge?.stop();
    bridge = null;
    if (endpointsDir !== null) {
      fs.rmSync(endpointsDir, { recursive: true, force: true });
      endpointsDir = null;
    }
  });

  async function startStack(executeDelayMs: number): Promise<{ pipeName: string }> {
    const pipeName = uniquePipeName('autodesk-mcp-e2e-progress');
    bridge = new FakeBridge({ pipeName, executeDelayMs, onExecute: () => okEnvelope({ done: true }) });
    await bridge.start();

    endpointsDir = fs.mkdtempSync(path.join(os.tmpdir(), 'autodesk-mcp-e2e-'));
    writeDescriptor(endpointsDir, pipeName);

    client = await RawMcpClient.connect(distIndex, {
      AUTODESK_MCP_ENDPOINTS_DIR: endpointsDir,
      AUTODESK_MCP_PREFERRED_PRODUCT: 'Civil3D',
      AUTODESK_MCP_PREFERRED_BRIDGE: 'Civil3D.Bridge',
      AUTODESK_MCP_ENDPOINTS_POLL_INTERVAL_MS: '50',
      AUTODESK_MCP_HEARTBEAT_INTERVAL_MS: '0',
    });
    return { pipeName };
  }

  it('forwards bridge progress notifications to the MCP client', async () => {
    await startStack(300);
    const progress: Array<{ progress?: number; message?: string }> = [];
    client!.onMessage((message) => {
      if (message.method === 'notifications/progress') {
        const params = message.params as { progress?: number; message?: string };
        progress.push({ progress: params.progress, message: params.message });
      }
    });

    const pending = client!.request('tools/call', {
      name: 'echo',
      arguments: { text: 'slow', _meta: { progressToken: 'tok-e2e' } },
    });

    // Wait for the bridge to receive the execute request, then stream progress.
    const start = Date.now();
    while (bridge!.requests.find((r) => r.method === 'tools/execute') === undefined && Date.now() - start < 5000) {
      await new Promise((resolve) => setTimeout(resolve, 20));
    }
    const executeRequest = bridge!.requests.find((r) => r.method === 'tools/execute');
    bridge!.sendProgress(executeRequest!.correlationId!, 40, 'running', 'step 1');

    const response = await pending;
    expect(response.error).toBeUndefined();

    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(progress.length).toBeGreaterThan(0);
    expect(progress[0]?.progress).toBe(40);
    expect(progress[0]?.message).toContain('running');
  });

  it('forwards client cancellation to the bridge as a $/cancel notification', async () => {
    await startStack(3000);
    const pending = client!.request('tools/call', { name: 'echo', arguments: { text: 'slow' } });
    pending.catch(() => undefined); // the abort surfaces as an MCP error; do not fail the test

    const start = Date.now();
    while (bridge!.requests.find((r) => r.method === 'tools/execute') === undefined && Date.now() - start < 5000) {
      await new Promise((resolve) => setTimeout(resolve, 20));
    }
    const executeRequest = bridge!.requests.find((r) => r.method === 'tools/execute');
    expect(executeRequest?.correlationId).toBeDefined();

    // The tools/call request is the most recent request this client sent; cancel exactly that id.
    const requestId = client!.requestIds.at(-1);
    expect(requestId).toBeDefined();
    client!.notify('notifications/cancelled', {
      requestId,
      reason: 'e2e cancellation test',
    });

    await new Promise((resolve) => setTimeout(resolve, 200));
    expect(bridge!.cancels).toContain(executeRequest!.correlationId);
  });
});
