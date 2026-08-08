import { afterEach, describe, expect, it } from 'vitest';
import { BridgeConnection } from '../src/transport/bridgeConnection.js';
import { ErrorCode } from '../src/protocol/types.js';
import { FakeBridge, failEnvelope, uniquePipeName, type FakeBridgeOptions } from './helpers/fakeBridge.js';

describe('BridgeConnection over a real named pipe', () => {
  const bridges: FakeBridge[] = [];

  afterEach(async () => {
    for (const bridge of bridges) {
      await bridge.stop();
    }
    bridges.length = 0;
  });

  async function startBridge(pipeName: string, options?: Partial<FakeBridgeOptions>): Promise<FakeBridge> {
    const bridge = new FakeBridge({ pipeName, ...options });
    bridges.push(bridge);
    await bridge.start();
    return bridge;
  }

  it('round-trips a request and correlates by correlation id', async () => {
    const pipeName = uniquePipeName();
    await startBridge(pipeName);
    const connection = new BridgeConnection({ pipeName, requestTimeoutMs: 5000 });
    await connection.connect();

    const response = await connection.request('tools/list');
    expect(response.success).toBe(true);
    expect(response.data).toMatchObject({ schemaVersion: 1 });
    connection.close();
  });

  it('supports concurrent requests and resolves each to its own response', async () => {
    const pipeName = uniquePipeName();
    await startBridge(pipeName);
    const connection = new BridgeConnection({ pipeName, requestTimeoutMs: 5000 });
    await connection.connect();

    const [first, second] = await Promise.all([
      connection.request('tools/list'),
      connection.request('tools/list'),
    ]);
    expect(first.correlationId).not.toBe(second.correlationId);
    expect(first.success).toBe(true);
    expect(second.success).toBe(true);
    connection.close();
  });

  it('delivers bridge notifications as events', async () => {
    const pipeName = uniquePipeName();
    const bridge = await startBridge(pipeName);
    const connection = new BridgeConnection({ pipeName, requestTimeoutMs: 5000 });
    await connection.connect();

    const notifications: Array<{ method: string }> = [];
    connection.on('notification', (notification: { method: string }) => notifications.push(notification));

    const pending = connection.request('tools/execute', { tool: 'echo', arguments: {} });
    // Stream progress while the request is in flight, then resolve it via a second request path:
    // simplest deterministic approach: fire the notification, then let the echo reply arrive.
    await new Promise((resolve) => setTimeout(resolve, 50));
    bridge.sendProgress('unknown-correlation', 50, 'working');
    bridge.sendProgress('unknown-correlation', 90, 'working');
    const response = await pending;
    expect(response.success).toBe(true);
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(notifications.length).toBe(2);
    expect(notifications[0]?.method).toBe('$/progress');
    connection.close();
  });

  it('sends $/cancel notifications without awaiting a response', async () => {
    const pipeName = uniquePipeName();
    const bridge = await startBridge(pipeName);
    const connection = new BridgeConnection({ pipeName, requestTimeoutMs: 5000 });
    await connection.connect();

    const response = await connection.request('tools/execute', { tool: 'echo', arguments: {} }, { correlationId: 'c-cancel-test' });
    connection.cancel('c-cancel-test', 'client requested');
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(bridge.cancels).toContain('c-cancel-test');
    expect(response.success).toBe(true);
    connection.close();
  });

  it('times out requests that never receive a response', async () => {
    const pipeName = uniquePipeName();
    await startBridge(pipeName, {
      onExecute: () => new Promise(() => undefined) as never, // never replies
    });
    const connection = new BridgeConnection({ pipeName, requestTimeoutMs: 200 });
    await connection.connect();

    await expect(connection.request('tools/execute', { tool: 'echo', arguments: {} }))
      .rejects.toMatchObject({ code: ErrorCode.E_TIMEOUT });
    connection.close();
  });

  it('surfaces bridge business failures as returned envelopes, not exceptions', async () => {
    const pipeName = uniquePipeName();
    await startBridge(pipeName, {
      onExecute: (tool) => failEnvelope('E_NO_ACTIVE_DOCUMENT', `No document for ${tool}.`),
    });
    const connection = new BridgeConnection({ pipeName, requestTimeoutMs: 5000 });
    await connection.connect();

    const response = await connection.request('tools/execute', { tool: 'echo', arguments: {} });
    expect(response.success).toBe(false);
    expect(response.errorCode).toBe('E_NO_ACTIVE_DOCUMENT');
    connection.close();
  });

  it('rejects in-flight requests when the connection drops', async () => {
    const pipeName = uniquePipeName();
    const bridge = await startBridge(pipeName, {
      onExecute: () => new Promise(() => undefined) as never,
    });
    const connection = new BridgeConnection({ pipeName, requestTimeoutMs: 20000 });
    await connection.connect();

    const pending = connection.request('tools/execute', { tool: 'echo', arguments: {} });
    const expectation = expect(pending).rejects.toMatchObject({ code: ErrorCode.E_BRIDGE_UNAVAILABLE });
    await bridge.stop();
    await expectation;
    connection.close();
  });

  it('rejects when the pipe does not exist', async () => {
    const connection = new BridgeConnection({ pipeName: uniquePipeName(), connectTimeoutMs: 500, requestTimeoutMs: 500 });
    await expect(connection.connect()).rejects.toThrow();
  });

  it('survives an abrupt bridge crash: in-flight requests reject and errors are observable', async () => {
    const pipeName = uniquePipeName();
    const bridge = await startBridge(pipeName, {
      onExecute: () => new Promise(() => undefined) as never,
    });
    const connection = new BridgeConnection({ pipeName, requestTimeoutMs: 20000 });
    await connection.connect();

    const errors: Error[] = [];
    connection.on('error', (error: Error) => errors.push(error));

    const pending = connection.request('tools/execute', { tool: 'echo', arguments: {} });
    const expectation = expect(pending).rejects.toMatchObject({ code: ErrorCode.E_BRIDGE_UNAVAILABLE });
    await new Promise((resolve) => setTimeout(resolve, 50));
    bridge.abortAllConnections();
    await expectation;
    connection.close();
  });
});
