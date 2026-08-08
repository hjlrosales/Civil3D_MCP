import { afterEach, describe, expect, it } from 'vitest';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { FakeBridge, sampleManifest, uniquePipeName } from '../../src/server/Autodesk.Mcp.Server/test/helpers/fakeBridge.js';
import { RawMcpClient } from '../helpers/rawClient.js';
import { distIndex } from '../helpers/harness.js';

describe('multi-instance bridge selection end to end', () => {
  const bridges: FakeBridge[] = [];
  let client: RawMcpClient | null = null;
  let endpointsDir: string | null = null;

  afterEach(async () => {
    client?.close();
    client = null;
    for (const bridge of bridges) {
      await bridge.stop();
    }
    bridges.length = 0;
    if (endpointsDir !== null) {
      fs.rmSync(endpointsDir, { recursive: true, force: true });
      endpointsDir = null;
    }
  });

  async function startBridge(manifestOverrides: Parameters<typeof sampleManifest>[0], product: string, bridgeName: string): Promise<FakeBridge> {
    const bridge = new FakeBridge({
      pipeName: uniquePipeName('autodesk-mcp-e2e-multi'),
      manifest: sampleManifest(manifestOverrides),
    });
    await bridge.start();
    bridges.push(bridge);
    return bridge;
  }

  function writeDescriptorFor(bridge: FakeBridge, product: string, bridgeName: string, startedUtc: string): string {
    const file = path.join(endpointsDir!, `${bridgeName}-${Date.now()}-${Math.random().toString(36).slice(2)}.json`);
    fs.writeFileSync(file, JSON.stringify({
      bridgeName,
      product,
      productVersion: '2026',
      bridgeVersion: '1.0.0',
      sdkVersion: '1.0.0',
      protocolVersion: '1.0.0',
      pipeName: bridge.pipeName,
      pid: process.pid,
      startedUtc,
    }), 'utf8');
    return file;
  }

  async function startServer(extraEnv: Record<string, string> = {}): Promise<void> {
    client = await RawMcpClient.connect(distIndex, {
      AUTODESK_MCP_ENDPOINTS_DIR: endpointsDir!,
      AUTODESK_MCP_ENDPOINTS_POLL_INTERVAL_MS: '50',
      AUTODESK_MCP_HEARTBEAT_INTERVAL_MS: '0',
      ...extraEnv,
    });
  }

  async function waitForTool(name: string, timeoutMs = 15_000): Promise<void> {
    const start = Date.now();
    while (Date.now() - start < timeoutMs) {
      const response = await client!.request('tools/list', {});
      const tools = (response.result as { tools?: Array<{ name: string }> }).tools ?? [];
      if (tools.some((tool) => tool.name === name)) {
        return;
      }
      await new Promise((resolve) => setTimeout(resolve, 100));
    }
    throw new Error(`Timed out waiting for tool '${name}'.`);
  }

  it('selects the most recently started bridge when several are registered', async () => {
    const oldBridge = await startBridge([{ name: 'legacy_tool' }], 'Civil3D', 'Civil3D.Bridge');
    const newBridge = await startBridge([{ name: 'newest_tool' }], 'Civil3D', 'Civil3D.Bridge');

    endpointsDir = fs.mkdtempSync(path.join(os.tmpdir(), 'autodesk-mcp-e2e-'));
    writeDescriptorFor(oldBridge, 'Civil3D', 'Civil3D.Bridge', '2020-01-01T00:00:00.000Z');
    writeDescriptorFor(newBridge, 'Civil3D', 'Civil3D.Bridge', new Date().toISOString());

    await startServer({ AUTODESK_MCP_PREFERRED_PRODUCT: 'Civil3D', AUTODESK_MCP_PREFERRED_BRIDGE: 'Civil3D.Bridge' });
    await waitForTool('newest_tool');

    // The newest bridge's manifest wins; the legacy tool never appears.
    const response = await client!.request('tools/list', {});
    const tools = (response.result as { tools?: Array<{ name: string }> }).tools ?? [];
    expect(tools.some((tool) => tool.name === 'newest_tool')).toBe(true);
    expect(tools.some((tool) => tool.name === 'legacy_tool')).toBe(false);
  });

  it('prefers the configured product and bridge name over recency', async () => {
    // A foreign product bridge started most recently, and a Civil3D bridge started earlier.
    const foreign = await startBridge([{ name: 'foreign_tool' }], 'LandXML', 'LandXML.Bridge');
    const civil = await startBridge([{ name: 'civil_tool' }], 'Civil3D', 'Civil3D.Bridge');

    endpointsDir = fs.mkdtempSync(path.join(os.tmpdir(), 'autodesk-mcp-e2e-'));
    writeDescriptorFor(foreign, 'LandXML', 'LandXML.Bridge', new Date().toISOString());
    writeDescriptorFor(civil, 'Civil3D', 'Civil3D.Bridge', '2021-06-01T00:00:00.000Z');

    await startServer({ AUTODESK_MCP_PREFERRED_PRODUCT: 'Civil3D', AUTODESK_MCP_PREFERRED_BRIDGE: 'Civil3D.Bridge' });
    await waitForTool('civil_tool');

    const response = await client!.request('tools/list', {});
    const tools = (response.result as { tools?: Array<{ name: string }> }).tools ?? [];
    expect(tools.some((tool) => tool.name === 'civil_tool')).toBe(true);
    expect(tools.some((tool) => tool.name === 'foreign_tool')).toBe(false);
  });
});
