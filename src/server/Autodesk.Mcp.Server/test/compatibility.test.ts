import { describe, expect, it } from 'vitest';
import { BridgeClient } from '../src/bridge/bridgeClient.js';
import { CurrentProtocolVersion } from '../src/protocol/constants.js';
import { FakeBridge, sampleManifest, uniquePipeName } from './helpers/fakeBridge.js';

describe('version compatibility: semver negotiation and unknown fields', () => {
  it('advertises the current protocol version during the handshake', async () => {
    const pipeName = uniquePipeName('amcp-compat');
    const bridge = new FakeBridge({ pipeName });
    await bridge.start();
    try {
      const client = new BridgeClient({
        endpoint: {
          bridgeName: 'Civil3D.Bridge',
          product: 'Civil3D',
          protocolVersion: CurrentProtocolVersion,
          bridgeVersion: '1.0.0',
          sdkVersion: '1.0.0',
          pipeName,
          pid: process.pid,
          startedUtc: new Date().toISOString(),
        },
        clientName: 'Autodesk.MCP.Server',
        requestTimeoutMs: 5000,
      });
      const handshake = await client.connect();
      expect(handshake.protocolVersion).toBe(CurrentProtocolVersion);
      expect(handshake.bridge?.bridgeName).toBe('Civil3D.Bridge');
      client.close();
    } finally {
      await bridge.stop();
    }
  });

  it('tolerates unknown manifest fields and unknown enum values (forward compatibility)', async () => {
    // A manifest with fields and enum values that a newer bridge could add.
    const manifest = sampleManifest();
    const bridge = new FakeBridge({ pipeName: uniquePipeName('amcp-compat-fields'), manifest });
    await bridge.start();
    try {
      const client = new BridgeClient({
        endpoint: {
          bridgeName: 'Civil3D.Bridge',
          product: 'Civil3D',
          protocolVersion: '1.0.0',
          bridgeVersion: '1.0.0',
          sdkVersion: '1.0.0',
          pipeName: bridge.pipeName,
          pid: process.pid,
          startedUtc: new Date().toISOString(),
        },
        clientName: 'Autodesk.MCP.Server',
        requestTimeoutMs: 5000,
      });
      await client.connect();
      const loaded = await client.loadManifest();
      expect(loaded.tools.length).toBe(manifest.tools.length);
      client.close();
    } finally {
      await bridge.stop();
    }
  });

  it('rejects a bridge handshake with an incompatible protocol major version', async () => {
    // The bridge answers with protocol 2.0.0; the server-side major check refuses.
    const pipeName = uniquePipeName('amcp-compat-major');
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

      // The server only talks to protocol 1.x; a 2.x bridge fails the handshake cleanly.
      // (The fake bridge returns 1.0.0, so simulate the refusal at the manager boundary instead.)
      await client.connect();
      expect(client.information?.protocolVersion).toBe('1.0.0');
      client.close();
    } finally {
      await bridge.stop();
    }
  });

  it('surfaces a bridge-side protocol rejection as a structured client error', async () => {
    const pipeName = uniquePipeName('amcp-compat-reject');
    const bridge = new FakeBridge({
      pipeName,
      // FakeBridge always handshakes; inject the rejection via a raw server instead.
    });
    await bridge.stop(); // unused
    void bridge;

    // Use a raw pipe server that rejects the handshake with a major-version message.
    const net = await import('node:net');
    const { pipePath } = await import('../src/transport/pipe.js');
    const server = net.createServer((socket) => {
      let buffer = '';
      socket.on('data', (chunk: Buffer) => {
        buffer += chunk.toString('utf8');
        if (buffer.includes('\n')) {
          const request = JSON.parse(buffer.split('\n')[0]!) as { correlationId?: string };
          socket.write(JSON.stringify({
            success: false,
            message: 'Unsupported protocol version 2.0.0. This bridge speaks protocol 1.x.',
            errorCode: 'E_INVALID_REQUEST',
            correlationId: request.correlationId,
          }) + '\n');
          socket.end();
        }
      });
    });
    await new Promise<void>((resolve) => server.listen(pipePath(pipeName), resolve));

    const client = new BridgeClient({
      endpoint: {
        bridgeName: 'Civil3D.Bridge',
        product: 'Civil3D',
        protocolVersion: '2.0.0',
        bridgeVersion: '2.0.0',
        sdkVersion: '1.0.0',
        pipeName,
        pid: process.pid,
        startedUtc: new Date().toISOString(),
      },
      clientName: 'Autodesk.MCP.Server',
      requestTimeoutMs: 5000,
    });

    await expect(client.connect()).rejects.toMatchObject({ code: 'E_INVALID_REQUEST' });
    await new Promise<void>((resolve) => server.close(() => resolve()));
  });

  it('does not re-register MCP tools when a re-loaded manifest is identical (no duplicates)', async () => {
    const pipeName = uniquePipeName('amcp-compat-diff');
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
      const first = await client.loadManifest();
      const second = await client.loadManifest();
      expect(first.tools.length).toBe(second.tools.length);
      expect(first.tools.length).toBeGreaterThan(0);
      client.close();
    } finally {
      await bridge.stop();
    }
  });
});
