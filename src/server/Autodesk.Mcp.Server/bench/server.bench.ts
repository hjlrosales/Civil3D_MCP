import { afterAll, beforeAll, bench } from 'vitest';
import { spawn, type ChildProcess } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
import { BridgeClient } from '../src/bridge/bridgeClient.js';
import { FakeBridge, sampleManifest, uniquePipeName, type Manifest, type ToolManifest } from '../test/helpers/fakeBridge.js';

/**
 * Server benchmark suite (vitest bench).
 *
 * Run with: npm run bench   (builds first, then: vitest bench --config vitest.bench.config.ts)
 *
 * Covers: startup (real process), handshake + tool discovery, large manifest loading
 * (500 tools) with diff-caching, workflow-style execute round-trips, reconnect after
 * bridge restart, and memory usage of the server process.
 */

const serverDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const distIndex = path.join(serverDir, 'dist', 'index.js');

interface ServerHarness {
  process: ChildProcess;
  transport: StdioClientTransport;
  client: Client;
  endpointsDir: string;
  bridge: FakeBridge;
}

let baselineHeap = 0;

beforeAll(() => {
  baselineHeap = process.memoryUsage().heapUsed;
});

/** Builds a manifest with `count` tools (plus the base echo/list tools). */
function largeManifest(count: number): Manifest {
  const tools: ToolManifest[] = [...sampleManifest().tools];
  for (let i = 0; i < count; i += 1) {
    tools.push({
      name: `bench_tool_${i}`,
      displayName: `Bench Tool ${i}`,
      description: `Benchmark tool ${i}.`,
      version: '1.0.0',
      permission: 'ReadOnly',
      risk: 'Low',
      timeoutMilliseconds: 30000,
      supportsProgress: false,
      supportsCancellation: false,
      inputSchema: {
        type: 'object',
        properties: { query: { type: 'string' } },
        required: ['query'],
        additionalProperties: false,
      },
    });
  }
  return { ...sampleManifest(), tools };
}

/** Writes an endpoint descriptor so the server can discover the fake bridge. */
function writeDescriptor(endpointsDir: string, pipeName: string): void {
  const descriptor = {
    bridgeName: 'Civil3D.Bridge',
    product: 'Civil3D',
    productVersion: '2026',
    bridgeVersion: '1.0.0',
    sdkVersion: '1.0.0',
    protocolVersion: '1.0.0',
    pipeName,
    pid: process.pid,
    startedUtc: new Date().toISOString(),
  };
  fs.writeFileSync(path.join(endpointsDir, `Civil3D-${process.pid}.json`), JSON.stringify(descriptor), 'utf8');
}

/** Spawns the real server binary and returns an MCP client connected to it. */
async function startServerHarness(bridge: FakeBridge): Promise<ServerHarness> {
  const endpointsDir = fs.mkdtempSync(path.join(os.tmpdir(), 'autodesk-mcp-bench-'));
  writeDescriptor(endpointsDir, bridge.pipeName);

  const child = spawn(process.execPath, [distIndex], {
    env: {
      ...process.env,
      AUTODESK_MCP_ENDPOINTS_DIR: endpointsDir,
      AUTODESK_MCP_PREFERRED_PRODUCT: 'Civil3D',
      AUTODESK_MCP_PREFERRED_BRIDGE: 'Civil3D.Bridge',
      AUTODESK_MCP_ENDPOINTS_POLL_INTERVAL_MS: '100',
      AUTODESK_MCP_RECONNECT_DELAY_MS: '100',
      AUTODESK_MCP_HEARTBEAT_INTERVAL_MS: '0',
    },
    stdio: ['pipe', 'pipe', 'pipe'],
  });

  const transport = new StdioClientTransport({
    command: process.execPath,
    args: [distIndex],
    env: {
      ...process.env,
      AUTODESK_MCP_ENDPOINTS_DIR: endpointsDir,
      AUTODESK_MCP_PREFERRED_PRODUCT: 'Civil3D',
      AUTODESK_MCP_PREFERRED_BRIDGE: 'Civil3D.Bridge',
      AUTODESK_MCP_ENDPOINTS_POLL_INTERVAL_MS: '100',
      AUTODESK_MCP_RECONNECT_DELAY_MS: '100',
      AUTODESK_MCP_HEARTBEAT_INTERVAL_MS: '0',
    },
  });

  const client = new Client({ name: 'autodesk-mcp-bench', version: '1.0.0' });
  await client.connect(transport);
  return { process: child, transport, client, endpointsDir, bridge };
}

async function closeHarness(harness: ServerHarness): Promise<void> {
  await harness.client.close();
  await harness.transport.close();
  harness.process.kill();
  fs.rmSync(harness.endpointsDir, { recursive: true, force: true });
}

/** In-process client connected to a fake bridge (handshake + manifest loaded). */
async function connectBridgeClient(pipeName: string, manifest: Manifest): Promise<BridgeClient> {
  const client = new BridgeClient({
    endpoint: {
      bridgeName: 'Civil3D.Bridge',
      product: 'Civil3D',
      productVersion: '2026',
      bridgeVersion: '1.0.0',
      sdkVersion: '1.0.0',
      protocolVersion: '1.0.0',
      pipeName,
      pid: process.pid,
      startedUtc: new Date().toISOString(),
    },
    clientName: 'autodesk-mcp-bench',
    clientVersion: '1.0.0',
    requestTimeoutMs: 30_000,
  });
  const bridge = new FakeBridge({ pipeName, manifest });
  await bridge.start();
  await client.connect();
  await client.loadManifest();
  return client;
}

// ---------------------------------------------------------------------------
// 1. Startup: real process spawn -> MCP initialize -> tools/list
// ---------------------------------------------------------------------------

bench('server startup -> initialize + tools/list (real process)', async () => {
  const bridge = new FakeBridge({ pipeName: uniquePipeName('autodesk-mcp-bench-start') });
  await bridge.start();
  const harness = await startServerHarness(bridge);
  try {
    const result = await harness.client.listTools();
    if (result.tools.length === 0) {
      throw new Error('No tools discovered.');
    }
  } finally {
    await closeHarness(harness);
    await bridge.stop();
  }
}, { iterations: 3, time: 0 });

// ---------------------------------------------------------------------------
// 2. Handshake + tool discovery (in-process, real pipe)
// ---------------------------------------------------------------------------

let discoveryClient: BridgeClient | null = null;

beforeAll(async () => {
  discoveryClient = await connectBridgeClient(uniquePipeName('autodesk-mcp-bench-disc'), sampleManifest());
});

bench('handshake + tools/list (in-process, real pipe)', async () => {
  if (discoveryClient === null) throw new Error('setup failed');
  await discoveryClient.loadManifest();
}, { iterations: 50, time: 0 });

// ---------------------------------------------------------------------------
// 3. Execute round-trips (workflow-style calls)
// ---------------------------------------------------------------------------

bench('tools/execute round-trip (echo)', async () => {
  if (discoveryClient === null) throw new Error('setup failed');
  const envelope = await discoveryClient.execute('echo', { text: 'benchmark' });
  if (!envelope.success) throw new Error('execute failed');
}, { iterations: 100, time: 0 });

// ---------------------------------------------------------------------------
// 4. Large manifest loading (500 tools) with diff-caching
// ---------------------------------------------------------------------------

let largeClient: BridgeClient | null = null;

beforeAll(async () => {
  largeClient = await connectBridgeClient(uniquePipeName('autodesk-mcp-bench-large'), largeManifest(500));
});

bench('large manifest load + diff (500 tools)', async () => {
  if (largeClient === null) throw new Error('setup failed');
  await largeClient.loadManifest();
}, { iterations: 10, time: 0 });

// ---------------------------------------------------------------------------
// 5. Reconnect after bridge restart
// ---------------------------------------------------------------------------

bench('reconnect (bridge restart -> connect + handshake + manifest)', async () => {
  const pipeName = uniquePipeName('autodesk-mcp-bench-reconnect');
  let bridge = new FakeBridge({ pipeName, manifest: sampleManifest() });
  await bridge.start();
  let client = await connectBridgeClient(pipeName, sampleManifest());
  // Simulate the bridge going away and returning on the same pipe name.
  await client.close();
  await bridge.stop();
  bridge = new FakeBridge({ pipeName, manifest: sampleManifest() });
  await bridge.start();
  client = await connectBridgeClient(pipeName, sampleManifest());
  await client.close();
  await bridge.stop();
}, { iterations: 5, time: 0 });

// ---------------------------------------------------------------------------
// 6. Memory usage
// ---------------------------------------------------------------------------

afterAll(async () => {
  await discoveryClient?.close();
  await largeClient?.close();
  const usage = process.memoryUsage();
  const deltaMb = (usage.heapUsed - baselineHeap) / (1024 * 1024);
  const rssMb = usage.rss / (1024 * 1024);
  // eslint-disable-next-line no-console
  console.log(`\n[bench] server process heap delta: ${deltaMb.toFixed(1)} MB; RSS: ${rssMb.toFixed(1)} MB`);
});
