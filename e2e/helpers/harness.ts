import { spawn, type ChildProcess } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
import { FakeBridge, sampleManifest, uniquePipeName } from '../../src/server/Autodesk.Mcp.Server/test/helpers/fakeBridge.js';
import type { Manifest } from '../../src/server/Autodesk.Mcp.Server/src/protocol/types.js';

/** Absolute path to the built server entry point. */
export const distIndex = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '..',
  '..',
  'src',
  'server',
  'Autodesk.Mcp.Server',
  'dist',
  'index.js',
);

export interface HarnessOptions {
  /** Overrides for server environment variables (e.g. preferredProduct). */
  env?: Record<string, string>;
  /** Fast polling/timing for tests. */
  fast?: boolean;
  /** Custom bridge manifest (defaults to the sample manifest). */
  manifest?: Manifest;
  /** A pre-built fake bridge to use instead of creating a new one. */
  bridge?: FakeBridge;
}

export interface Harness {
  bridge: FakeBridge;
  pipeName: string;
  endpointsDir: string;
  child: ChildProcess;
  transport: StdioClientTransport;
  client: Client;
}

/** Writes an endpoint descriptor for the given pipe, PID and start time. */
export function writeDescriptor(endpointsDir: string, pipeName: string, pid = process.pid, startedUtc?: string): string {
  const file = path.join(endpointsDir, `Civil3D-${pid}-${Math.random().toString(36).slice(2)}.json`);
  const descriptor = {
    bridgeName: 'Civil3D.Bridge',
    product: 'Civil3D',
    productVersion: '2026',
    bridgeVersion: '1.0.0',
    sdkVersion: '1.0.0',
    protocolVersion: '1.0.0',
    pipeName,
    pid,
    startedUtc: startedUtc ?? new Date().toISOString(),
  };
  fs.writeFileSync(file, JSON.stringify(descriptor), 'utf8');
  return file;
}

/**
 * Starts a fake bridge + a real server process + an MCP client, all wired together
 * through the endpoint registry (exactly like production discovery).
 */
export async function startHarness(options: HarnessOptions = {}): Promise<Harness> {
  const pipeName = options.bridge?.pipeName ?? uniquePipeName('autodesk-mcp-e2e');
  const bridge = options.bridge ?? new FakeBridge({ pipeName, manifest: options.manifest ?? sampleManifest() });
  if (options.bridge === undefined) {
    await bridge.start();
  }

  const endpointsDir = fs.mkdtempSync(path.join(os.tmpdir(), 'autodesk-mcp-e2e-'));
  writeDescriptor(endpointsDir, pipeName);

  const baseEnv = {
    AUTODESK_MCP_ENDPOINTS_DIR: endpointsDir,
    AUTODESK_MCP_PREFERRED_PRODUCT: 'Civil3D',
    AUTODESK_MCP_PREFERRED_BRIDGE: 'Civil3D.Bridge',
    AUTODESK_MCP_HEARTBEAT_INTERVAL_MS: '0',
    ...(options.fast === true
      ? {
          AUTODESK_MCP_ENDPOINTS_POLL_INTERVAL_MS: '50',
          AUTODESK_MCP_RECONNECT_DELAY_MS: '50',
        }
      : {}),
    ...options.env,
  };

  // NOTE: two server processes are spawned on purpose.
  //   - `child` is kept so tests can signal the process directly (SIGTERM shutdown) and assert
  //     on exit codes; it also connects to the bridge like any real client would.
  //   - the StdioClientTransport spawns its own process, which is the peer the MCP `client`
  //     actually talks to (the transport owns that child's lifecycle).
  // Both share the same endpoints registry, so discovery behaves exactly as in production.
  const child = spawn(process.execPath, [distIndex], {
    env: { ...process.env, ...baseEnv },
    stdio: ['pipe', 'pipe', 'pipe'],
  });

  const transport = new StdioClientTransport({
    command: process.execPath,
    args: [distIndex],
    env: { ...process.env, ...baseEnv },
  });
  const client = new Client({ name: 'autodesk-mcp-e2e', version: '1.0.0' });
  await client.connect(transport);

  return { bridge, pipeName, endpointsDir, child, transport, client };
}

/** Closes the MCP client, kills the server process, cleans the registry and stops the bridge. */
export async function stopHarness(harness: Harness): Promise<void> {
  try {
    await harness.client.close();
  } catch {
    // ignore
  }
  try {
    await harness.transport.close();
  } catch {
    // ignore
  }
  harness.child.kill();
  try {
    fs.rmSync(harness.endpointsDir, { recursive: true, force: true });
  } catch {
    // ignore
  }
  await harness.bridge.stop();
}

/** Polls until `predicate` returns true or the timeout elapses. */
export async function waitFor(predicate: () => boolean | Promise<boolean>, timeoutMs = 10_000, intervalMs = 100): Promise<void> {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (await predicate()) {
      return;
    }
    await new Promise((resolve) => setTimeout(resolve, intervalMs));
  }
  throw new Error(`Timed out after ${timeoutMs} ms waiting for condition.`);
}

/** Waits until the server has discovered at least one tool from the bridge. */
export async function waitForTools(client: Client, timeoutMs = 15_000): Promise<void> {
  await waitFor(async () => {
    const result = await client.listTools();
    return result.tools.length > 0;
  }, timeoutMs);
}
