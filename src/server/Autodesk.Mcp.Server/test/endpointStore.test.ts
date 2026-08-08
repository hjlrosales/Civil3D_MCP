import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { cleanupStaleEndpoints, isProcessAlive, parseEndpointDescriptor, scanEndpoints, selectEndpoint } from '../src/discovery/endpointStore.js';
import type { EndpointDescriptor } from '../src/protocol/types.js';

function descriptor(overrides: Partial<EndpointDescriptor> = {}): EndpointDescriptor {
  return {
    bridgeName: 'Civil3D.Bridge',
    product: 'Civil3D',
    productVersion: '2026',
    bridgeVersion: '1.0.0',
    sdkVersion: '1.0.0',
    protocolVersion: '1.0.0',
    pipeName: 'autodesk-mcp-civil3d-123',
    pid: process.pid,
    startedUtc: new Date().toISOString(),
    ...overrides,
  };
}

describe('endpoint discovery', () => {
  let dir: string;

  beforeEach(() => {
    dir = fs.mkdtempSync(path.join(os.tmpdir(), 'amcp-endpoints-'));
  });

  afterEach(() => {
    fs.rmSync(dir, { recursive: true, force: true });
  });

  function writeDescriptor(fileName: string, data: EndpointDescriptor): void {
    fs.writeFileSync(path.join(dir, fileName), JSON.stringify(data));
  }

  it('parses a descriptor written by the C# registrar (wire names pid/startedUtc)', () => {
    const parsed = parseEndpointDescriptor({
      bridgeName: 'Civil3D.Bridge',
      product: 'Civil3D',
      pipeName: 'autodesk-mcp-civil3d-123',
      protocolVersion: '1.0.0',
      pid: 42,
      startedUtc: '2026-01-01T00:00:00Z',
    });
    expect(parsed).not.toBeNull();
    expect(parsed!.pid).toBe(42);
    expect(parsed!.startedUtc).toBe('2026-01-01T00:00:00Z');
  });

  it('rejects descriptors missing required fields', () => {
    expect(parseEndpointDescriptor({ bridgeName: 'x' })).toBeNull();
    expect(parseEndpointDescriptor({ product: 'x', pipeName: 'p', pid: 1 })).toBeNull();
    expect(parseEndpointDescriptor('nope')).toBeNull();
  });

  it('scans the registry directory and skips malformed files', () => {
    writeDescriptor('Civil3D-100.json', descriptor({ pipeName: 'autodesk-mcp-a' }));
    writeDescriptor('AutoCAD-200.json', descriptor({ bridgeName: 'AutoCAD.Bridge', product: 'AutoCAD', pipeName: 'autodesk-mcp-b' }));
    fs.writeFileSync(path.join(dir, 'broken.json'), '{ not json');
    fs.writeFileSync(path.join(dir, 'notes.txt'), 'ignored');

    const endpoints = scanEndpoints(dir);
    expect(endpoints).toHaveLength(2);
    expect(endpoints.map((endpoint) => endpoint.product).sort()).toEqual(['AutoCAD', 'Civil3D']);
  });

  it('treats the current process as alive and a dead pid as stale', () => {
    expect(isProcessAlive(process.pid)).toBe(true);
    expect(isProcessAlive(999_999_999)).toBe(false);
    expect(isProcessAlive(0)).toBe(false);
  });

  it('cleans up stale descriptor files whose pid is dead', () => {
    writeDescriptor('Civil3D-live.json', descriptor({ pipeName: 'autodesk-mcp-live' }));
    writeDescriptor('Civil3D-dead.json', descriptor({ pid: 999_999_999, pipeName: 'autodesk-mcp-dead' }));
    fs.writeFileSync(path.join(dir, 'partial.json'), '{ "bridgeName":'); // partially-written: left alone

    const removed = cleanupStaleEndpoints(dir);
    expect(removed).toBe(1);
    expect(fs.existsSync(path.join(dir, 'Civil3D-live.json'))).toBe(true);
    expect(fs.existsSync(path.join(dir, 'Civil3D-dead.json'))).toBe(false);
    expect(fs.existsSync(path.join(dir, 'partial.json'))).toBe(true);
  });

  it('selects the most recently started endpoint', () => {
    const older = descriptor({ pipeName: 'pipe-a', startedUtc: '2026-01-01T00:00:00Z' });
    const newer = descriptor({ pipeName: 'pipe-b', startedUtc: '2026-02-01T00:00:00Z' });
    expect(selectEndpoint([older, newer])?.pipeName).toBe('pipe-b');
  });

  it('prefers the configured product and bridge name', () => {
    const civil = descriptor({ product: 'Civil3D', bridgeName: 'Civil3D.Bridge', pipeName: 'pipe-c' });
    const otherCivil = descriptor({ product: 'Civil3D', bridgeName: 'Civil3D.Bridge.Alt', pipeName: 'pipe-d' });
    const acad = descriptor({ product: 'AutoCAD', bridgeName: 'AutoCAD.Bridge', pipeName: 'pipe-e', startedUtc: '2026-03-01T00:00:00Z' });

    const selected = selectEndpoint([civil, otherCivil, acad], { preferredProduct: 'Civil3D', preferredBridge: 'Civil3D.Bridge' });
    expect(selected?.pipeName).toBe('pipe-c');
  });

  it('returns null when no live endpoint exists', () => {
    const dead = descriptor({ pid: 999_999_999 });
    expect(selectEndpoint([dead])).toBeNull();
    expect(selectEndpoint([])).toBeNull();
  });
});
