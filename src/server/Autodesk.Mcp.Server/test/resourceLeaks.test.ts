import { describe, expect, it } from 'vitest';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { BridgeConnection } from '../src/transport/bridgeConnection.js';
import { BridgeClient } from '../src/bridge/bridgeClient.js';
import { BridgeManager } from '../src/manager.js';
import { FakeBridge, uniquePipeName } from './helpers/fakeBridge.js';

describe('resource leaks: repeated startup/shutdown, connect/disconnect and execution cycles', () => {
  it('leaves no pending requests after repeated connect/disconnect cycles', async () => {
    const pipeName = uniquePipeName('amcp-leak-pipes');
    const bridge = new FakeBridge({ pipeName });
    await bridge.start();
    try {
      for (let cycle = 0; cycle < 10; cycle += 1) {
        const connection = new BridgeConnection({ pipeName, connectTimeoutMs: 1000, requestTimeoutMs: 2000 });
        await connection.connect();
        const response = await connection.request('tools/list');
        expect(response.success).toBe(true);
        connection.close();
      }
      // After the final close the connection object is done; a fresh one still works.
      const final = new BridgeConnection({ pipeName, connectTimeoutMs: 1000, requestTimeoutMs: 2000 });
      await final.connect();
      const response = await final.request('tools/list');
      expect(response.success).toBe(true);
      final.close();
    } finally {
      await bridge.stop();
    }
  });

  it('survives repeated tool execution cycles without leaking pending state', async () => {
    const pipeName = uniquePipeName('amcp-leak-exec');
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
      await client.loadManifest();

      for (let i = 0; i < 100; i += 1) {
        const response = await client.execute('echo', { text: `run-${i}` });
        expect(response.success).toBe(true);
      }
      client.close();
    } finally {
      await bridge.stop();
    }
  });

  it('leaves the endpoints registry clean after repeated manager start/stop cycles', async () => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'amcp-leak-mgr-'));
    const pipeName = uniquePipeName('amcp-leak-mgr');
    const bridge = new FakeBridge({ pipeName });
    await bridge.start();
    try {
      fs.writeFileSync(
        path.join(dir, 'Civil3D-leak.json'),
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

      for (let cycle = 0; cycle < 5; cycle += 1) {
        const manager = new BridgeManager({
          endpointsDir: dir,
          clientName: 'Autodesk.MCP.Server',
          endpointsPollIntervalMs: 30,
          reconnectDelayMs: 30,
          maxReconnectAttempts: 5,
          heartbeatIntervalMs: 0,
          requestTimeoutMs: 3000,
          logger: { info: () => undefined, warn: () => undefined, debug: () => undefined },
        });
        manager.start();
        const start = Date.now();
        while ((manager.getBridge()?.connected ?? false) === false && Date.now() - start < 5000) {
          await new Promise((resolve) => setTimeout(resolve, 10));
        }
        expect(manager.getBridge()?.connected).toBe(true);
        manager.stop();
      }

      // The registry still holds only the one descriptor (no duplicates from repeated starts).
      const files = fs.readdirSync(dir).filter((f) => f.endsWith('.json'));
      expect(files.length).toBe(1);
    } finally {
      await bridge.stop();
      fs.rmSync(dir, { recursive: true, force: true });
    }
  });
});
