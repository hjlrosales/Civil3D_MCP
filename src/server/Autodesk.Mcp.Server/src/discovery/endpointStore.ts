import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { EndpointRegistryRelativePath } from '../protocol/constants.js';
import type { EndpointDescriptor } from '../protocol/types.js';

export interface EndpointLogger {
  info(message: string, ...args: any[]): void;
  warn(message: string, ...args: any[]): void;
  debug(message: string, ...args: any[]): void;
}

/** Resolves the platform endpoints directory (defaults to %LOCALAPPDATA%\AutodeskMcp\endpoints). */
export function defaultEndpointsDir(): string {
  const localAppData = process.env.LOCALAPPDATA;
  const base = localAppData && localAppData.length > 0
    ? localAppData
    : path.join(os.homedir(), 'AppData', 'Local');
  return path.join(base, EndpointRegistryRelativePath);
}

/** True when the operating system process is alive (any error other than ESRCH/ENOENT counts as alive). */
export function isProcessAlive(pid: number): boolean {
  if (!Number.isInteger(pid) || pid <= 0) {
    return false;
  }
  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    const code = (error as NodeJS.ErrnoException).code;
    return code === 'ESRCH' || code === 'ENOENT' ? false : true;
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function readString(value: unknown): string | undefined {
  return typeof value === 'string' && value.length > 0 ? value : undefined;
}

function readNumber(value: unknown): number | undefined {
  return typeof value === 'number' && Number.isFinite(value) ? value : undefined;
}

/**
 * Parses a raw endpoint descriptor file into a typed descriptor. Tolerates unknown fields;
 * returns null when the document is not a usable discovery record.
 */
export function parseEndpointDescriptor(raw: unknown): EndpointDescriptor | null {
  if (!isRecord(raw)) {
    return null;
  }
  const bridgeName = readString(raw.bridgeName);
  const product = readString(raw.product);
  const pipeName = readString(raw.pipeName);
  const pid = readNumber(raw.pid);
  if (bridgeName === undefined || product === undefined || pipeName === undefined || pid === undefined) {
    return null;
  }

  return {
    bridgeName,
    product,
    productVersion: readString(raw.productVersion),
    bridgeVersion: readString(raw.bridgeVersion) ?? '0.0.0',
    sdkVersion: readString(raw.sdkVersion) ?? '0.0.0',
    protocolVersion: readString(raw.protocolVersion) ?? '0.0.0',
    pipeName,
    pid,
    startedUtc: readString(raw.startedUtc) ?? new Date(0).toISOString(),
    lastHeartbeatAtUtc: readString(raw.lastHeartbeatAtUtc),
    capabilities: isRecord(raw.capabilities) ? (raw.capabilities as unknown as EndpointDescriptor['capabilities']) : undefined,
  };
}

/** Scans the endpoints directory and returns every parseable descriptor (malformed files are skipped). */
export function scanEndpoints(directory: string, logger?: EndpointLogger): EndpointDescriptor[] {
  let entries: string[];
  try {
    entries = fs.readdirSync(directory);
  } catch {
    return []; // The registry does not exist yet; this is normal before any bridge starts.
  }

  const endpoints: EndpointDescriptor[] = [];
  for (const entry of entries) {
    if (!entry.endsWith('.json')) {
      continue;
    }
    const filePath = path.join(directory, entry);
    try {
      const raw = JSON.parse(fs.readFileSync(filePath, 'utf8'));
      const descriptor = parseEndpointDescriptor(raw);
      if (descriptor === null) {
        logger?.warn('Skipping malformed endpoint descriptor %s.', filePath);
        continue;
      }
      endpoints.push(descriptor);
    } catch {
      logger?.warn('Skipping unreadable endpoint descriptor %s.', filePath);
    }
  }
  return endpoints;
}

/**
 * Removes descriptor files whose owning process is no longer alive (crashed bridges leave
 * stale files behind). Only files that parsed successfully are considered for deletion.
 */
export function cleanupStaleEndpoints(directory: string, logger?: EndpointLogger): number {
  let entries: string[];
  try {
    entries = fs.readdirSync(directory);
  } catch {
    return 0;
  }

  let removed = 0;
  for (const entry of entries) {
    if (!entry.endsWith('.json')) {
      continue;
    }
    const filePath = path.join(directory, entry);
    try {
      const descriptor = parseEndpointDescriptor(JSON.parse(fs.readFileSync(filePath, 'utf8')));
      if (descriptor !== null && !isProcessAlive(descriptor.pid)) {
        fs.unlinkSync(filePath);
        removed += 1;
        logger?.info('Removed stale endpoint descriptor %s (pid %d is not alive).', filePath, descriptor.pid);
      }
    } catch {
      // Unreadable or partially-written files are left alone.
    }
  }
  return removed;
}

export interface EndpointPreferences {
  /** Restrict candidates to this product (for example Civil3D). */
  preferredProduct?: string;
  /** Prefer the endpoint with this logical bridge name (for example Civil3D.Bridge). */
  preferredBridge?: string;
}

/**
 * Selects the endpoint to connect to when several bridges are running. Filtering order:
 * preferred product, then preferred bridge name, then the most recently started endpoint.
 * Returns null when no usable endpoint exists.
 */
export function selectEndpoint(endpoints: EndpointDescriptor[], preferences: EndpointPreferences = {}): EndpointDescriptor | null {
  let candidates = endpoints;
  if (preferences.preferredProduct !== undefined && preferences.preferredProduct.length > 0) {
    const byProduct = candidates.filter((endpoint) => endpoint.product === preferences.preferredProduct);
    if (byProduct.length > 0) {
      candidates = byProduct;
    }
  }

  if (preferences.preferredBridge !== undefined && preferences.preferredBridge.length > 0) {
    const byBridge = candidates.filter((endpoint) => endpoint.bridgeName === preferences.preferredBridge);
    if (byBridge.length > 0) {
      candidates = byBridge;
    }
  }

  const alive = candidates.filter((endpoint) => isProcessAlive(endpoint.pid));
  if (alive.length === 0) {
    return null;
  }

  return [...alive].sort((left, right) => {
    const leftTime = Date.parse(left.startedUtc);
    const rightTime = Date.parse(right.startedUtc);
    const byStart = (Number.isFinite(rightTime) ? rightTime : 0) - (Number.isFinite(leftTime) ? leftTime : 0);
    if (byStart !== 0) {
      return byStart;
    }
    return left.pipeName.localeCompare(right.pipeName);
  })[0] ?? null;
}

/** Stable fingerprint of an endpoint for change detection. */
export function endpointFingerprint(endpoint: EndpointDescriptor): string {
  return `${endpoint.bridgeName}|${endpoint.product}|${endpoint.pipeName}|${endpoint.pid}`;
}
