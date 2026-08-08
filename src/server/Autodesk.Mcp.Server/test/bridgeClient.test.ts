import { afterEach, describe, expect, it } from 'vitest';
import { BridgeClient, diffManifests } from '../src/bridge/bridgeClient.js';
import type { EndpointDescriptor, Manifest } from '../src/protocol/types.js';
import { FakeBridge, failEnvelope, okEnvelope, uniquePipeName, type FakeBridgeOptions } from './helpers/fakeBridge.js';

function endpoint(pipeName: string): EndpointDescriptor {
  return {
    bridgeName: 'Civil3D.Bridge',
    product: 'Civil3D',
    protocolVersion: '1.0.0',
    bridgeVersion: '1.0.0',
    sdkVersion: '1.0.0',
    pipeName,
    pid: process.pid,
    startedUtc: new Date().toISOString(),
  };
}

describe('BridgeClient', () => {
  const bridges: FakeBridge[] = [];

  afterEach(async () => {
    for (const bridge of bridges) {
      await bridge.stop();
    }
    bridges.length = 0;
  });

  async function startClient(pipeName: string, options?: Partial<FakeBridgeOptions>): Promise<{ bridge: FakeBridge; client: BridgeClient }> {
    const bridge = new FakeBridge({ pipeName, ...options });
    bridges.push(bridge);
    await bridge.start();
    const client = new BridgeClient({ endpoint: endpoint(pipeName), clientName: 'Autodesk.MCP.Server', clientVersion: '1.0.0', requestTimeoutMs: 5000 });
    await client.connect();
    return { bridge, client };
  }

  it('handshakes, captures the session id and advertises client capabilities', async () => {
    const pipeName = uniquePipeName();
    const { bridge, client } = await startClient(pipeName);

    expect(client.currentSessionId).toMatch(/^sess-/);
    expect(client.information?.bridgeName).toBe('Civil3D.Bridge');
    const handshake = bridge.requests.find((request) => request.method === 'handshake');
    expect(handshake?.params).toMatchObject({ clientName: 'Autodesk.MCP.Server', protocolVersion: '1.0.0' });
    client.close();
  });

  it('loads the manifest via tools/list', async () => {
    const pipeName = uniquePipeName();
    const { client } = await startClient(pipeName);

    const manifest = await client.loadManifest();
    expect(manifest.tools.map((tool) => tool.name)).toEqual(['drawing_info', 'echo', 'rename_alignment']);
    expect(client.currentManifest).toBe(manifest);
    client.close();
  });

  it('echoes the session id on subsequent requests', async () => {
    const pipeName = uniquePipeName();
    const { bridge, client } = await startClient(pipeName);
    await client.loadManifest();

    const execute = bridge.requests.find((request) => request.method === 'tools/execute');
    expect(execute).toBeUndefined();
    await client.execute('echo', { text: 'hi' });
    const toolCall = bridge.requests.find((request) => request.method === 'tools/execute');
    expect(toolCall?.params).toMatchObject({ tool: 'echo', arguments: { text: 'hi' } });
    client.close();
  });

  it('returns bridge failures as envelopes', async () => {
    const pipeName = uniquePipeName();
    const { client } = await startClient(pipeName, {
      onExecute: () => failEnvelope('E_OBJECT_NOT_FOUND', 'Not found.'),
    });

    const response = await client.execute('missing', {});
    expect(response.success).toBe(false);
    expect(response.errorCode).toBe('E_OBJECT_NOT_FOUND');
    client.close();
  });

  it('forwards $/cancel for an in-flight execution', async () => {
    const pipeName = uniquePipeName();
    const { bridge, client } = await startClient(pipeName, {
      onExecute: () => new Promise((resolve) => setTimeout(() => resolve(okEnvelope({ ok: true })), 200)),
    });

    const pending = client.execute('echo', {}, { correlationId: 'c1' });
    client.cancel('c1', 'stop');
    await pending;
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(bridge.cancels).toContain('c1');
    client.close();
  });

  it('emits progress notifications as typed events', async () => {
    const pipeName = uniquePipeName();
    const { bridge, client } = await startClient(pipeName, {
      onExecute: () => new Promise((resolve) => setTimeout(() => resolve(okEnvelope({ done: true })), 200)),
    });
    const progress: unknown[] = [];
    client.on('progress', (notification: unknown) => progress.push(notification));

    const pending = client.execute('echo', {}, { correlationId: 'c-progress' });
    bridge.sendProgress('c-progress', 25, 'collecting');
    await pending;
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(progress).toHaveLength(1);
    expect(progress[0]).toMatchObject({ correlationId: 'c-progress', percent: 25 });
    client.close();
  });
});

describe('diffManifests', () => {
  function manifestWith(versions: Record<string, string>): Manifest {
    return {
      schemaVersion: 1,
      protocolVersion: '1.0.0',
      generatedAtUtc: new Date().toISOString(),
      tools: Object.entries(versions).map(([name, version]) => ({
        name,
        displayName: name,
        description: '',
        version,
        timeoutMilliseconds: 30000,
        inputSchema: { type: 'object' },
      })),
    };
  }

  it('classifies added, removed and changed tools', () => {
    const previous = manifestWith({ a: '1.0.0', b: '1.0.0', c: '1.0.0' });
    const current = manifestWith({ a: '1.0.0', b: '1.1.0', d: '1.0.0' });

    const change = diffManifests(previous, current);
    expect(change.added.map((tool) => tool.name)).toEqual(['d']);
    expect(change.removed.map((tool) => tool.name)).toEqual(['c']);
    expect(change.changed.map((tool) => tool.name)).toEqual(['b']);
  });

  it('reports everything as added when there is no previous manifest', () => {
    const current = manifestWith({ a: '1.0.0' });
    const change = diffManifests(null, current);
    expect(change.added).toHaveLength(1);
    expect(change.removed).toHaveLength(0);
    expect(change.changed).toHaveLength(0);
  });

  it('reports no changes for an identical manifest', () => {
    const manifest = manifestWith({ a: '1.0.0' });
    const change = diffManifests(manifest, manifestWith({ a: '1.0.0' }));
    expect(change.added).toHaveLength(0);
    expect(change.removed).toHaveLength(0);
    expect(change.changed).toHaveLength(0);
  });
});
