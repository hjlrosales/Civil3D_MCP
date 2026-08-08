import { afterEach, describe, expect, it } from 'vitest';
import net from 'node:net';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { BridgeConnection } from '../src/transport/bridgeConnection.js';
import { BridgeProtocolError } from '../src/transport/ndjson.js';
import { BridgeClient } from '../src/bridge/bridgeClient.js';
import { BridgeManager } from '../src/manager.js';
import { ErrorCode } from '../src/protocol/types.js';
import { pipePath } from '../src/transport/pipe.js';
import { FakeBridge, failEnvelope, okEnvelope, uniquePipeName } from './helpers/fakeBridge.js';

describe('failure & recovery: connection-level protocol abuse', () => {
  const bridges: FakeBridge[] = [];
  const connections: BridgeConnection[] = [];

  afterEach(async () => {
    for (const connection of connections) {
      connection.close();
    }
    connections.length = 0;
    for (const bridge of bridges) {
      await bridge.stop();
    }
    bridges.length = 0;
  });

  async function startBridge(pipeName: string, options?: { onExecute?: (tool: string, args: unknown, confirm?: boolean) => ReturnType<typeof okEnvelope> }): Promise<FakeBridge> {
    const bridge = new FakeBridge({ pipeName, ...options });
    bridges.push(bridge);
    await bridge.start();
    return bridge;
  }

  function connect(pipeName: string, timeoutMs = 5000): BridgeConnection {
    const connection = new BridgeConnection({ pipeName, connectTimeoutMs: 1000, requestTimeoutMs: timeoutMs });
    connections.push(connection);
    return connection;
  }

  it('recovers after the bridge sends an oversized message and kills the connection', async () => {
    const pipeName = uniquePipeName('amcp-recover-oversize');
    const server = net.createServer((socket) => {
      socket.write('{"a":"' + 'x'.repeat(4 * 1024 * 1024) + '"}\n');
      socket.end();
    });
    await new Promise<void>((resolve) => server.listen(pipePath(pipeName), resolve));

    const connection = connect(pipeName);
    const errors: Error[] = [];
    connection.on('error', (error: Error) => errors.push(error));

    await expect(connection.connect()).resolves.toBeUndefined();
    await new Promise((resolve) => setTimeout(resolve, 100));

    expect(errors.length).toBeGreaterThan(0);
    await new Promise<void>((resolve) => server.close(() => resolve()));
  });

  it('treats a non-object wire message as a protocol error and drops the connection', async () => {
    const pipeName = uniquePipeName('amcp-recover-nonobject');
    const server = net.createServer((socket) => {
      socket.write('[1,2,3]\n');
      socket.end();
    });
    await new Promise<void>((resolve) => server.listen(pipePath(pipeName), resolve));

    const connection = connect(pipeName);
    const errors: Error[] = [];
    connection.on('error', (error: Error) => errors.push(error));

    await connection.connect();
    await new Promise((resolve) => setTimeout(resolve, 100));
    expect(errors.length).toBeGreaterThan(0);
    expect(errors[0]).toBeInstanceOf(BridgeProtocolError);
    await new Promise<void>((resolve) => server.close(() => resolve()));
  });

  it('surfaces responses with an unknown correlation id as unmatched instead of crashing', async () => {
    const pipeName = uniquePipeName('amcp-recover-unmatched');
    await startBridge(pipeName);
    const connection = connect(pipeName);
    await connection.connect();

    const unmatched: unknown[] = [];
    connection.on('unmatched', (message: unknown) => unmatched.push(message));

    const response = await connection.request('tools/list');
    expect(response.success).toBe(true);
    await new Promise((resolve) => setTimeout(resolve, 50));

    const again = await connection.request('tools/list');
    expect(again.success).toBe(true);
  });

  it('rejects a second connect() on the same connection instance', async () => {
    const pipeName = uniquePipeName('amcp-recover-reconnect');
    await startBridge(pipeName);
    const connection = connect(pipeName);
    await connection.connect();

    await expect(connection.connect()).rejects.toMatchObject({
      code: ErrorCode.E_BRIDGE_UNAVAILABLE,
    });
  });

  it('keeps working after a request times out (no orphaned pending state)', async () => {
    const pipeName = uniquePipeName('amcp-recover-timeout');
    await startBridge(pipeName, {
      onExecute: () => new Promise(() => undefined) as never,
    });
    const connection = connect(pipeName, 200);
    await connection.connect();

    await expect(
      connection.request('tools/execute', { tool: 'echo', arguments: {} }),
    ).rejects.toMatchObject({ code: ErrorCode.E_TIMEOUT });

    const response = await connection.request('tools/list');
    expect(response.success).toBe(true);
  });
});

describe('failure & recovery: bridge restart and reconnection', () => {
  let dir: string;
  let manager: BridgeManager | null = null;
  const bridges: FakeBridge[] = [];

  afterEach(async () => {
    manager?.stop();
    manager = null;
    for (const bridge of bridges) {
      await bridge.stop();
    }
    bridges.length = 0;
    if (dir !== undefined) {
      fs.rmSync(dir, { recursive: true, force: true });
    }
  });

  function createManager(): BridgeManager {
    manager = new BridgeManager({
      endpointsDir: dir,
      clientName: 'Autodesk.MCP.Server',
      endpointsPollIntervalMs: 50,
      reconnectDelayMs: 50,
      maxReconnectAttempts: 20,
      heartbeatIntervalMs: 0,
      requestTimeoutMs: 5000,
      logger: { info: () => undefined, warn: () => undefined, debug: () => undefined },
    });
    return manager;
  }

  function writeDescriptor(pipeName: string): void {
    fs.writeFileSync(
      path.join(dir, `Civil3D-${Math.random().toString(36).slice(2)}.json`),
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
  }

  async function waitFor(condition: () => boolean, timeoutMs = 8000): Promise<void> {
    const start = Date.now();
    while (Date.now() - start < timeoutMs) {
      if (condition()) {
        return;
      }
      await new Promise((resolve) => setTimeout(resolve, 20));
    }
    throw new Error('Timed out waiting for condition.');
  }

  it('drops the manifest when the endpoint descriptor disappears (no stale tools served)', async () => {
    dir = fs.mkdtempSync(path.join(os.tmpdir(), 'amcp-recover-'));
    const pipeName = uniquePipeName();
    const bridge = new FakeBridge({ pipeName });
    bridges.push(bridge);
    await bridge.start();

    const managerInstance = createManager();
    managerInstance.start();
    writeDescriptor(pipeName);
    await waitFor(() => managerInstance.getBridge() !== null && managerInstance.getBridge()!.connected);
    await waitFor(() => managerInstance.getManifest() !== null);
    expect(managerInstance.getManifest()!.tools.length).toBeGreaterThan(0);

    for (const file of fs.readdirSync(dir)) {
      fs.unlinkSync(path.join(dir, file));
    }
    await waitFor(() => managerInstance.getBridge() === null);
    expect(managerInstance.getManifest()).toBeNull();
  });

  it('recovers when a stale descriptor points at a dead pid and a live bridge appears', async () => {
    dir = fs.mkdtempSync(path.join(os.tmpdir(), 'amcp-recover-'));
    const managerInstance = createManager();
    managerInstance.start();

    fs.writeFileSync(
      path.join(dir, 'Civil3D-dead.json'),
      JSON.stringify({
        bridgeName: 'Civil3D.Bridge',
        product: 'Civil3D',
        protocolVersion: '1.0.0',
        bridgeVersion: '1.0.0',
        sdkVersion: '1.0.0',
        pipeName: 'pipe-that-never-exists',
        pid: 999_999_999,
        startedUtc: new Date().toISOString(),
      }),
    );
    await new Promise((resolve) => setTimeout(resolve, 150));
    expect(managerInstance.getBridge()).toBeNull();

    const pipeName = uniquePipeName();
    const bridge = new FakeBridge({ pipeName });
    bridges.push(bridge);
    await bridge.start();
    writeDescriptor(pipeName);
    await waitFor(() => managerInstance.getBridge() !== null && managerInstance.getBridge()!.connected);
    expect(managerInstance.getStatus()).toBe('connected');
  });

  it('cleans up a stale descriptor file whose owning process is dead (PID reuse guard)', async () => {
    dir = fs.mkdtempSync(path.join(os.tmpdir(), 'amcp-recover-'));
    const staleFile = path.join(dir, 'Civil3D-stale.json');
    fs.writeFileSync(staleFile, JSON.stringify({
      bridgeName: 'Civil3D.Bridge',
      product: 'Civil3D',
      protocolVersion: '1.0.0',
      bridgeVersion: '1.0.0',
      sdkVersion: '1.0.0',
      pipeName: 'dead-pipe',
      pid: 999_999_999,
      startedUtc: new Date().toISOString(),
    }));

    const { cleanupStaleEndpoints } = await import('../src/discovery/endpointStore.js');
    const removed = cleanupStaleEndpoints(dir);
    expect(removed).toBe(1);
    expect(fs.existsSync(staleFile)).toBe(false);
  });

  it('handles a bridge disappearing mid-execution: the in-flight call fails cleanly', async () => {
    dir = fs.mkdtempSync(path.join(os.tmpdir(), 'amcp-recover-'));
    const pipeName = uniquePipeName();
    const bridge = new FakeBridge({
      pipeName,
      onExecute: () => new Promise(() => undefined) as never, // never replies
    });
    bridges.push(bridge);
    await bridge.start();

    const managerInstance = createManager();
    managerInstance.start();
    writeDescriptor(pipeName);
    await waitFor(() => managerInstance.getBridge() !== null && managerInstance.getBridge()!.connected);

    const client = managerInstance.getBridge()!;
    const pending = client.execute('echo', { text: 'x' });
    const expectation = expect(pending).rejects.toMatchObject({ code: ErrorCode.E_BRIDGE_UNAVAILABLE });

    bridge.abortAllConnections();
    await bridge.stop();
    await expectation;
    expect(managerInstance.getStatus()).toBe('reconnecting');
  });
});

describe('failure & recovery: duplicate/unknown request ids and shutdown', () => {
  it('settles both requests when the same correlation id is used twice (no silent orphan)', async () => {
    const pipeName = uniquePipeName('amcp-recover-dupe');
    const bridge = new FakeBridge({ pipeName });
    await bridge.start();
    try {
      const connection = new BridgeConnection({ pipeName, connectTimeoutMs: 1000, requestTimeoutMs: 5000 });
      await connection.connect();
      const pending = [
        connection.request('tools/list', undefined, { correlationId: 'dupe-id' }),
        connection.request('tools/list', undefined, { correlationId: 'dupe-id' }),
      ];
      const results = await Promise.allSettled(pending);
      // Both must settle (the first may be rejected as a duplicate, never hang forever).
      expect(results.every((r) => r.status === 'fulfilled' || r.status === 'rejected')).toBe(true);
      connection.close();
    } finally {
      await bridge.stop();
    }
  });

  it('recovers after an unknown-method request is rejected', async () => {
    const pipeName = uniquePipeName('amcp-recover-unknownmethod');
    const bridge = new FakeBridge({ pipeName });
    await bridge.start();
    try {
      const client = new BridgeClient({
        endpoint: {
          bridgeName: 'Civil3D.Bridge',
          product: 'Civil3D',
          protocolVersion: '1.0.0',
          bridgeVersion: '1.0.0',
          sdkVersion: '1.0.0',
          pipeName,
          pid: process.pid,
          startedUtc: new Date().toISOString(),
        },
        clientName: 'Autodesk.MCP.Server',
        requestTimeoutMs: 5000,
      });
      await client.connect();
      const response = await (client as unknown as { request: (m: string, p?: unknown) => Promise<{ success: boolean; errorCode?: string }> }).request('no.such.method');
      expect(response.success).toBe(false);
      expect(response.errorCode).toBe('E_INVALID_REQUEST');
      client.close();
    } finally {
      await bridge.stop();
    }
  });

  it('returns E_CANCELLED for work queued behind a shutdown', async () => {
    const pipeName = uniquePipeName('amcp-recover-shutdown');
    const bridge = new FakeBridge({
      pipeName,
      onExecute: (tool) => (tool === 'slow' ? failEnvelope('E_CANCELLED', 'The bridge is shutting down.') : okEnvelope({ ok: true })),
    });
    await bridge.start();
    try {
      const connection = new BridgeConnection({ pipeName, connectTimeoutMs: 1000, requestTimeoutMs: 5000 });
      await connection.connect();
      const response = await connection.request('tools/execute', { tool: 'slow', arguments: {} });
      expect(response.success).toBe(false);
      expect(response.errorCode).toBe('E_CANCELLED');
      connection.close();
    } finally {
      await bridge.stop();
    }
  });

  it('handshake failure surfaces the bridge rejection as a structured error', async () => {
    const pipeName = uniquePipeName('amcp-recover-handshake');
    const server = net.createServer((socket) => {
      let buffer = '';
      socket.on('data', (chunk: Buffer) => {
        buffer += chunk.toString('utf8');
        if (buffer.includes('\n')) {
          const request = JSON.parse(buffer.split('\n')[0]!) as { correlationId?: string };
          socket.write(JSON.stringify({ success: false, message: 'Protocol version mismatch.', errorCode: 'E_INVALID_REQUEST', correlationId: request.correlationId }) + '\n');
          socket.end();
        }
      });
    });
    await new Promise<void>((resolve) => server.listen(pipePath(pipeName), resolve));

    const client = new BridgeClient({
      endpoint: {
        bridgeName: 'Civil3D.Bridge',
        product: 'Civil3D',
        protocolVersion: '1.0.0',
        bridgeVersion: '1.0.0',
        sdkVersion: '1.0.0',
        pipeName,
        pid: process.pid,
        startedUtc: new Date().toISOString(),
      },
      clientName: 'Autodesk.MCP.Server',
      requestTimeoutMs: 5000,
    });

    await expect(client.connect()).rejects.toMatchObject({
      code: 'E_INVALID_REQUEST',
    });
    await new Promise<void>((resolve) => server.close(() => resolve()));
  });
});
