import { afterEach, describe, expect, it } from 'vitest';
import fs from 'node:fs';
import path from 'node:path';
import { LifecycleHarness, waitForEvent, waitUntil } from './helpers/lifecycleHarness.js';
import { sampleManifest, uniquePipeName } from './helpers/fakeBridge.js';
import type { Manifest, ToolManifest } from '../src/protocol/types.js';

/**
 * Persistence & restart lifecycle: the MCP client must converge on the correct tool list for
 * every ordering of "VS Code starts" and "Civil 3D starts", and must recover from every
 * disconnect without the user restarting anything. These tests pin the behaviour that made
 * "Discovered 0 tools" permanent: tools/list is answered once, immediately after initialize,
 * so the server must actively notify the client whenever the catalog changes.
 */
describe('bridge lifecycle: discovery, reconnect and tool-list propagation', () => {
  let harness: LifecycleHarness | null = null;

  afterEach(async () => {
    await harness?.dispose();
    harness = null;
  });

  function createHarness(): LifecycleHarness {
    harness = new LifecycleHarness();
    return harness;
  }

  /** A manifest with a distinct tool set, for asserting that a refresh actually happened. */
  function manifestWith(names: string[]): Manifest {
    const base = sampleManifest().tools[0] as ToolManifest;
    return {
      schemaVersion: 1,
      protocolVersion: '1.0.0',
      generatedAtUtc: new Date().toISOString(),
      tools: names.map((name) => ({ ...base, name, displayName: name })),
    };
  }

  // -- The regression that caused "Discovered 0 tools" ------------------------------------

  it('declares the tools.listChanged capability so clients subscribe to catalog updates', async () => {
    const test = createHarness();
    await test.startServer();

    const capabilities = test.mcpClient.getServerCapabilities();
    expect(capabilities?.tools).toMatchObject({ listChanged: true });
  });

  it('notifies the client when the manifest arrives after tools/list already returned empty', async () => {
    const test = createHarness();
    const bridge = await test.startBridge();

    // The MCP client connects and lists tools before any bridge is discovered - exactly what
    // VS Code does immediately after initialize. This first answer is legitimately empty.
    await test.startServer();
    expect(await test.toolNames()).toEqual([]);

    // Arm the wait before publishing the endpoint so the assertion is event-driven.
    const notified = waitUntil(() => test.toolListChanges.length > 0, 8_000, 'tools/list_changed');
    test.writeEndpoint('Civil3D-1.json', { pipeName: bridge.pipeName });

    await notified;
    expect(await test.toolNames()).toEqual([...bridge.toolNames].sort());
  });

  it('serves the full catalog when Civil 3D was already running before the server started', async () => {
    const test = createHarness();
    const bridge = await test.startBridge();
    test.writeEndpoint('Civil3D-1.json', { pipeName: bridge.pipeName });

    await test.startServer();
    await test.waitForToolCount(bridge.toolNames.length);
    expect(await test.toolNames()).toEqual([...bridge.toolNames].sort());
  });

  // -- Restart orderings -------------------------------------------------------------------

  it('recovers when VS Code restarts while Civil 3D keeps running', async () => {
    const test = createHarness();
    const bridge = await test.startBridge();
    test.writeEndpoint('Civil3D-1.json', { pipeName: bridge.pipeName });

    await test.startServer();
    await test.waitForToolCount(bridge.toolNames.length);

    // VS Code restarts: the whole server stack is torn down and started again. The bridge and
    // its endpoint descriptor are untouched, as they would be with Civil 3D still open.
    await test.stopServer();
    await test.startServer();

    await test.waitForToolCount(bridge.toolNames.length);
    expect(await test.toolNames()).toEqual([...bridge.toolNames].sort());
  });

  it('reconnects and refreshes the catalog when Civil 3D restarts under a running server', async () => {
    const test = createHarness();
    const first = await test.startBridge(uniquePipeName(), manifestWith(['alpha', 'beta']));
    test.writeEndpoint('Civil3D-1.json', { pipeName: first.pipeName, startedUtc: new Date(Date.now() - 60_000).toISOString() });

    await test.startServer();
    await test.waitForToolCount(2);
    expect(await test.toolNames()).toEqual(['alpha', 'beta']);

    // Civil 3D closes: the bridge stops and its descriptor is removed.
    await test.stopBridge(first);
    test.removeEndpoint('Civil3D-1.json');
    await test.waitForToolCount(0);

    // Civil 3D restarts under a new pid, on a new pipe, publishing a different catalog.
    const second = await test.startBridge(uniquePipeName(), manifestWith(['alpha', 'gamma', 'delta']));
    test.writeEndpoint('Civil3D-2.json', { pipeName: second.pipeName });

    await test.waitForToolNames(['alpha', 'delta', 'gamma']);
  });

  it('clears the advertised tools when Civil 3D closes so tools/list never lies', async () => {
    const test = createHarness();
    const bridge = await test.startBridge();
    test.writeEndpoint('Civil3D-1.json', { pipeName: bridge.pipeName });

    await test.startServer();
    await test.waitForToolCount(bridge.toolNames.length);
    const changesBefore = test.toolListChanges.length;

    await test.stopBridge(bridge);
    test.removeEndpoint('Civil3D-1.json');

    await test.waitForToolCount(0);
    expect(test.toolListChanges.length).toBeGreaterThan(changesBefore);
    expect(test.bridgeManager.getStatus()).toBe('discovering');
  });

  // -- Drop / reappear ---------------------------------------------------------------------

  it('reconnects automatically after the bridge connection drops with the endpoint still live', async () => {
    const test = createHarness();
    const bridge = await test.startBridge();
    test.writeEndpoint('Civil3D-1.json', { pipeName: bridge.pipeName });

    await test.startServer();
    await test.waitForToolCount(bridge.toolNames.length);

    // The pipe dies but Civil 3D stays up: the descriptor remains and the listener still accepts.
    bridge.abortAllConnections();

    await waitUntil(
      () => test.bridgeManager.getBridge()?.connected === true,
      8_000,
      'the manager to re-establish the bridge connection',
    );
    await test.waitForToolCount(bridge.toolNames.length);
  });

  it('keeps rediscovering after the reconnect budget is exhausted (no permanent give-up)', async () => {
    const test = createHarness();
    // The descriptor points at a pipe with no listener: every attempt fails until the budget is
    // spent. Before the fix the manager parked in 'offline' forever, because the endpoint
    // fingerprint never changed and nothing re-armed discovery.
    const pipeName = uniquePipeName();
    test.writeEndpoint('Civil3D-1.json', { pipeName });

    await test.startServer({ maxReconnectAttempts: 2, reconnectDelayMs: 10, retryCooldownMs: 50 });
    await waitUntil(() => test.bridgeManager.getStatus() === 'discovering', 8_000, 'the retry budget to be exhausted');

    // The very same endpoint becomes reachable; no descriptor change, no server restart.
    const bridge = await test.startBridge(pipeName);
    await test.waitForToolCount(bridge.toolNames.length);
    expect(test.bridgeManager.getStatus()).toBe('connected');
  });

  it('does not busy-loop against an unreachable endpoint', async () => {
    const test = createHarness();
    test.writeEndpoint('Civil3D-1.json', { pipeName: uniquePipeName() });

    const attempts: string[] = [];
    await test.startServer({ maxReconnectAttempts: 1, reconnectDelayMs: 10, retryCooldownMs: 400 });
    test.bridgeManager.on('status', (status: string) => attempts.push(status));

    await new Promise((resolve) => setTimeout(resolve, 1_200));
    const connectAttempts = attempts.filter((status) => status === 'connecting' || status === 'reconnecting').length;
    // With a 400 ms cooldown and a 25 ms poll interval, a busy loop would produce dozens.
    expect(connectAttempts).toBeLessThan(15);
  });

  // -- Registry hygiene --------------------------------------------------------------------

  it('ignores a stale descriptor whose process is dead and connects to the live bridge', async () => {
    const test = createHarness();
    test.writeEndpoint('Civil3D-dead.json', { pipeName: 'pipe-that-never-listens', pid: 999_999_999 });

    const bridge = await test.startBridge();
    test.writeEndpoint('Civil3D-live.json', { pipeName: bridge.pipeName });

    await test.startServer();
    await test.waitForToolCount(bridge.toolNames.length);
    // The stale descriptor is reaped by the monitor rather than left to confuse discovery.
    await waitUntil(
      () => !fs.existsSync(path.join(test.endpointsDir, 'Civil3D-dead.json')),
      8_000,
      'the stale descriptor to be removed',
    );
  });

  it('survives a descriptor that is rewritten in place with a new pipe (PID reuse)', async () => {
    const test = createHarness();
    const first = await test.startBridge(uniquePipeName(), manifestWith(['alpha']));
    test.writeEndpoint('Civil3D-1.json', { pipeName: first.pipeName, startedUtc: new Date(Date.now() - 60_000).toISOString() });

    await test.startServer();
    await test.waitForToolCount(1);

    // The same pid re-registers on a different pipe (Civil 3D restarted into a recycled pid).
    await test.stopBridge(first);
    const second = await test.startBridge(uniquePipeName(), manifestWith(['beta', 'gamma']));
    test.writeEndpoint('Civil3D-1.json', { pipeName: second.pipeName });

    await test.waitForToolNames(['beta', 'gamma']);
  });

  it('selects the most recently started bridge when several Civil 3D instances are running', async () => {
    const test = createHarness();
    const older = await test.startBridge(uniquePipeName(), manifestWith(['older_tool']));
    const newer = await test.startBridge(uniquePipeName(), manifestWith(['newer_tool']));
    test.writeEndpoint('Civil3D-old.json', { pipeName: older.pipeName, startedUtc: new Date(Date.now() - 120_000).toISOString() });
    test.writeEndpoint('Civil3D-new.json', { pipeName: newer.pipeName, startedUtc: new Date().toISOString() });

    await test.startServer();
    await test.waitForToolCount(1);
    expect(await test.toolNames()).toEqual(['newer_tool']);
    expect(test.bridgeManager.getEndpoint()?.pipeName).toBe(newer.pipeName);
  });

  it('fails over to the remaining instance when the selected one disappears', async () => {
    const test = createHarness();
    const survivor = await test.startBridge(uniquePipeName(), manifestWith(['survivor_tool']));
    const doomed = await test.startBridge(uniquePipeName(), manifestWith(['doomed_tool']));
    test.writeEndpoint('Civil3D-survivor.json', { pipeName: survivor.pipeName, startedUtc: new Date(Date.now() - 120_000).toISOString() });
    test.writeEndpoint('Civil3D-doomed.json', { pipeName: doomed.pipeName, startedUtc: new Date().toISOString() });

    await test.startServer();
    await test.waitForToolCount(1);
    expect(await test.toolNames()).toEqual(['doomed_tool']);

    await test.stopBridge(doomed);
    test.removeEndpoint('Civil3D-doomed.json');

    await test.waitForToolNames(['survivor_tool']);
    expect(test.bridgeManager.getEndpoint()?.pipeName).toBe(survivor.pipeName);
  });

  // -- Civil 3D absent ---------------------------------------------------------------------

  it('starts healthy with no bridge at all and connects when Civil 3D appears later', async () => {
    const test = createHarness();
    await test.startServer();

    expect(await test.toolNames()).toEqual([]);
    expect(test.bridgeManager.getStatus()).toBe('discovering');

    const bridge = await test.startBridge();
    test.writeEndpoint('Civil3D-1.json', { pipeName: bridge.pipeName });

    await test.waitForToolCount(bridge.toolNames.length);
    expect(test.bridgeManager.getStatus()).toBe('connected');
  });

  it('reports a clear error from tools/call while no bridge is connected', async () => {
    const test = createHarness();
    await test.startServer();

    await expect(test.mcpClient.callTool({ name: 'drawing_info', arguments: {} })).rejects.toThrow(/no bridge/i);
  });

  // -- Shutdown ----------------------------------------------------------------------------

  it('shuts down gracefully without leaking timers, sockets or the endpoint watcher', async () => {
    const test = createHarness();
    const bridge = await test.startBridge();
    test.writeEndpoint('Civil3D-1.json', { pipeName: bridge.pipeName });

    await test.startServer({ heartbeatIntervalMs: 25 });
    await test.waitForToolCount(bridge.toolNames.length);

    const manager = test.bridgeManager;
    const before = process.getActiveResourcesInfo().length;
    await test.stopServer();

    expect(manager.getStatus()).toBe('offline');
    expect(manager.getBridge()).toBeNull();

    // Timers and pipes are released, so the handle count does not grow after shutdown.
    await new Promise((resolve) => setTimeout(resolve, 150));
    expect(process.getActiveResourcesInfo().length).toBeLessThanOrEqual(before);
  });

  it('stops cleanly while a connection attempt is still in flight', async () => {
    const test = createHarness();
    test.writeEndpoint('Civil3D-1.json', { pipeName: uniquePipeName() });

    await test.startServer({ reconnectDelayMs: 10 });
    const stopping = waitForEvent(test.bridgeManager, 'status', 4_000).catch(() => undefined);
    await test.stopServer();
    await stopping;
    // No unhandled rejection and no reconnect after stop.
    await new Promise((resolve) => setTimeout(resolve, 200));
  });
});
