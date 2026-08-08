import { afterEach, describe, expect, it } from 'vitest';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { FakeBridge, uniquePipeName } from '../../src/server/Autodesk.Mcp.Server/test/helpers/fakeBridge.js';
import { startHarness, stopHarness, waitForTools, writeDescriptor, type Harness } from '../helpers/harness.js';

describe('bridge restart and reconnect end to end', () => {
  let harness: Harness | null = null;

  afterEach(async () => {
    if (harness !== null) {
      await stopHarness(harness);
      harness = null;
    }
  });

  it('reconnects to a restarted bridge and serves the new manifest', async () => {
    harness = await startHarness({ fast: true });
    await waitForTools(harness.client);
    expect((await harness.client.listTools()).tools.length).toBeGreaterThan(0);

    // Crash the bridge, remove its descriptor, and give the server time to notice.
    const oldPipe = harness.bridge.pipeName;
    await harness.bridge.stop();
    fs.rmSync(path.join(harness.endpointsDir, 'Civil3D-Bridge.json'), { force: true });
    for (const file of fs.readdirSync(harness.endpointsDir)) {
      fs.rmSync(path.join(harness.endpointsDir, file), { force: true });
    }
    await new Promise((resolve) => setTimeout(resolve, 300));

    // Restart the bridge on a fresh pipe and republish its endpoint.
    const newBridge = new FakeBridge({ pipeName: uniquePipeName('autodesk-mcp-e2e-reconnect') });
    await newBridge.start();
    writeDescriptor(harness.endpointsDir, newBridge.pipeName);

    // The server should pick up the new descriptor and reconnect automatically.
    const start = Date.now();
    while (Date.now() - start < 15_000) {
      try {
        const result = await harness.client.listTools();
        if (result.tools.length > 0) {
          break;
        }
      } catch {
        // listTools may throw while the manifest is briefly empty; keep polling
      }
      await new Promise((resolve) => setTimeout(resolve, 100));
    }

    const result = await harness.client.listTools();
    expect(result.tools.length).toBeGreaterThan(0);
    expect(result.tools.some((tool) => tool.name === 'echo')).toBe(true);
    expect(oldPipe).not.toBe(newBridge.pipeName);

    await newBridge.stop();
  });
});
