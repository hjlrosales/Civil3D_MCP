import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { BridgeManager } from '../src/manager.js';
import type { EndpointDescriptor } from '../src/protocol/types.js';
import { FakeBridge, uniquePipeName } from './helpers/fakeBridge.js';

describe('BridgeManager', () => {
  let dir: string;
  const bridges: FakeBridge[] = [];
  let manager: BridgeManager | null = null;

  beforeEach(() => {
    dir = fs.mkdtempSync(path.join(os.tmpdir(), 'amcp-mgr-'));
  });

  afterEach(async () => {
    manager?.stop();
    manager = null;
    for (const bridge of bridges) {
      await bridge.stop();
    }
    bridges.length = 0;
    fs.rmSync(dir, { recursive: true, force: true });
  });

  function writeDescriptor(bridge: FakeBridge, overrides: Partial<EndpointDescriptor> = {}): void {
    const descriptor: EndpointDescriptor = {
      bridgeName: 'Civil3D.Bridge',
      product: 'Civil3D',
      protocolVersion: '1.0.0',
      bridgeVersion: '1.0.0',
      sdkVersion: '1.0.0',
      pipeName: bridge.pipeName,
      pid: process.pid,
      startedUtc: new Date().toISOString(),
      ...overrides,
    };
    fs.writeFileSync(path.join(dir, `${descriptor.product}-${descriptor.pipeName}.json`), JSON.stringify(descriptor));
  }

  function createManager(options: Partial<ConstructorParameters<typeof BridgeManager>[0]> = {}): BridgeManager {
    manager = new BridgeManager({
      endpointsDir: dir,
      clientName: 'Autodesk.MCP.Server',
      endpointsPollIntervalMs: 100,
      reconnectDelayMs: 50,
      maxReconnectAttempts: 5,
      heartbeatIntervalMs: 0,
      requestTimeoutMs: 5000,
      ...options,
    });
    return manager;
  }

  function waitFor(condition: () => boolean, timeoutMs = 5000): Promise<void> {
    return new Promise((resolve, reject) => {
      const start = Date.now();
      const timer = setInterval(() => {
        if (condition()) {
          clearInterval(timer);
          resolve();
        } else if (Date.now() - start > timeoutMs) {
          clearInterval(timer);
          reject(new Error('Timed out waiting for condition.'));
        }
      }, 20);
    });
  }

  it('connects to a bridge once its descriptor appears and loads the manifest', async () => {
    const pipeName = uniquePipeName();
    const bridge = new FakeBridge({ pipeName });
    bridges.push(bridge);
    await bridge.start();

    const managerInstance = createManager();
    managerInstance.start();
    writeDescriptor(bridge);

    await waitFor(() => managerInstance.getBridge() !== null && managerInstance.getBridge()!.connected);
    await waitFor(() => managerInstance.getManifest() !== null);
    expect(managerInstance.getStatus()).toBe('connected');
    expect(managerInstance.getManifest()!.tools.length).toBeGreaterThan(0);
  });

  it('reconnects with backoff after the bridge restarts', async () => {
    const pipeName = uniquePipeName();
    let bridge = new FakeBridge({ pipeName });
    bridges.push(bridge);
    await bridge.start();

    const managerInstance = createManager();
    managerInstance.start();
    writeDescriptor(bridge);
    await waitFor(() => managerInstance.getBridge() !== null);

    // Simulate a bridge crash: close the server, then restart it on the same pipe.
    await bridge.stop();
    await waitFor(() => managerInstance.getStatus() === 'reconnecting' || managerInstance.getBridge() === null);
    bridge = new FakeBridge({ pipeName });
    bridges[0] = bridge;
    await bridge.start();

    await waitFor(() => managerInstance.getBridge() !== null && managerInstance.getBridge()!.connected);
    expect(managerInstance.getStatus()).toBe('connected');
  });

  it('stays offline until a bridge appears, then connects', async () => {
    const pipeName = uniquePipeName();
    const managerInstance = createManager();
    managerInstance.start();
    expect(managerInstance.getStatus()).toBe('discovering');

    const bridge = new FakeBridge({ pipeName });
    bridges.push(bridge);
    await bridge.start();
    writeDescriptor(bridge);

    await waitFor(() => managerInstance.getBridge() !== null && managerInstance.getBridge()!.connected);
    expect(managerInstance.getStatus()).toBe('connected');
  });

  it('selects the preferred product when several bridges are registered', async () => {
    const preferred = uniquePipeName('preferred');
    const other = uniquePipeName('other');
    const preferredBridge = new FakeBridge({ pipeName: preferred });
    const otherBridge = new FakeBridge({ pipeName: other });
    bridges.push(preferredBridge, otherBridge);
    await preferredBridge.start();
    await otherBridge.start();

    const managerInstance = createManager({ preferences: { preferredProduct: 'Civil3D' } });
    managerInstance.start();
    writeDescriptor(preferredBridge, { product: 'Civil3D' });
    writeDescriptor(otherBridge, { product: 'AutoCAD', pipeName: other });

    await waitFor(() => managerInstance.getBridge() !== null && managerInstance.getBridge()!.connected);
    expect(managerInstance.getBridge()!.endpoint.pipeName).toBe(preferred);
    expect(managerInstance.getBridge()!.endpoint.product).toBe('Civil3D');
  });
});
