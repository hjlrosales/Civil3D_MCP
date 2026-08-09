import fs from 'node:fs';
import path from 'node:path';
import { defaultEndpointsDir } from './discovery/endpointStore.js';
import type { EndpointPreferences } from './discovery/endpointStore.js';

/** Runtime configuration of the MCP server. */
export interface ServerConfig {
  /** pino log level (trace|debug|info|warn|error|fatal). */
  logLevel: string;
  /** Override for the endpoints registry directory. */
  endpointsDir: string;
  /** Restrict discovery to this product (for example Civil3D). */
  preferredProduct?: string;
  /** Prefer the endpoint with this logical bridge name. */
  preferredBridge?: string;
  /** Base reconnect delay in milliseconds (doubles on each failed attempt). */
  reconnectDelayMs: number;
  /** Reconnect attempts per burst before the endpoint is parked for the cooldown (0 = never park). */
  maxReconnectAttempts: number;
  /** How long an unreachable-but-registered endpoint is left alone before discovery retries it. */
  retryCooldownMs: number;
  /** Per-request timeout in milliseconds. */
  requestTimeoutMs: number;
  /** Heartbeat interval in milliseconds (0 disables). */
  heartbeatIntervalMs: number;
  /** Endpoints registry polling interval in milliseconds. */
  endpointsPollIntervalMs: number;
  /** Client identity reported during the handshake. */
  clientName: string;
  clientVersion: string;
}

const DEFAULTS: ServerConfig = {
  logLevel: 'info',
  endpointsDir: defaultEndpointsDir(),
  reconnectDelayMs: 1_000,
  maxReconnectAttempts: 10,
  retryCooldownMs: 30_000,
  requestTimeoutMs: 30_000,
  heartbeatIntervalMs: 15_000,
  endpointsPollIntervalMs: 3_000,
  clientName: 'Autodesk.MCP.Server',
  clientVersion: '1.0.0',
};

function readNumber(value: unknown, fallback: number): number {
  const parsed = typeof value === 'number' ? value : typeof value === 'string' ? Number(value) : Number.NaN;
  return Number.isFinite(parsed) ? parsed : fallback;
}

function readString(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim().length > 0 ? value.trim() : undefined;
}

/** Loads a JSON configuration file; returns null when missing or malformed. */
export function loadConfigFile(filePath: string): Partial<ServerConfig> | null {
  try {
    const raw = JSON.parse(fs.readFileSync(filePath, 'utf8')) as Record<string, unknown>;
    const partial: Partial<ServerConfig> = {};
    if (readString(raw.logLevel) !== undefined) {
      partial.logLevel = readString(raw.logLevel);
    }
    if (readString(raw.endpointsDir) !== undefined) {
      partial.endpointsDir = path.resolve(readString(raw.endpointsDir) as string);
    }
    if (readString(raw.preferredProduct) !== undefined) {
      partial.preferredProduct = readString(raw.preferredProduct);
    }
    if (readString(raw.preferredBridge) !== undefined) {
      partial.preferredBridge = readString(raw.preferredBridge);
    }
    if (raw.reconnectDelayMs !== undefined) {
      partial.reconnectDelayMs = readNumber(raw.reconnectDelayMs, DEFAULTS.reconnectDelayMs);
    }
    if (raw.maxReconnectAttempts !== undefined) {
      partial.maxReconnectAttempts = readNumber(raw.maxReconnectAttempts, DEFAULTS.maxReconnectAttempts);
    }
    if (raw.retryCooldownMs !== undefined) {
      partial.retryCooldownMs = readNumber(raw.retryCooldownMs, DEFAULTS.retryCooldownMs);
    }
    if (raw.requestTimeoutMs !== undefined) {
      partial.requestTimeoutMs = readNumber(raw.requestTimeoutMs, DEFAULTS.requestTimeoutMs);
    }
    if (raw.heartbeatIntervalMs !== undefined) {
      partial.heartbeatIntervalMs = readNumber(raw.heartbeatIntervalMs, DEFAULTS.heartbeatIntervalMs);
    }
    if (raw.endpointsPollIntervalMs !== undefined) {
      partial.endpointsPollIntervalMs = readNumber(raw.endpointsPollIntervalMs, DEFAULTS.endpointsPollIntervalMs);
    }
    if (readString(raw.clientName) !== undefined) {
      partial.clientName = readString(raw.clientName);
    }
    if (readString(raw.clientVersion) !== undefined) {
      partial.clientVersion = readString(raw.clientVersion);
    }
    return partial;
  } catch {
    return null;
  }
}

/** Loads configuration from defaults, then a config file, then environment variables. */
export function loadConfig(filePath?: string): ServerConfig {
  const fromFile = filePath !== undefined ? loadConfigFile(filePath) : null;
  const config: ServerConfig = { ...DEFAULTS, ...(fromFile ?? {}) };

  const env = process.env;
  const logLevel = readString(env.AUTODESK_MCP_LOG_LEVEL);
  const endpointsDir = readString(env.AUTODESK_MCP_ENDPOINTS_DIR);
  const preferredProduct = readString(env.AUTODESK_MCP_PREFERRED_PRODUCT);
  const preferredBridge = readString(env.AUTODESK_MCP_PREFERRED_BRIDGE);
  if (logLevel !== undefined) {
    config.logLevel = logLevel;
  }
  if (endpointsDir !== undefined) {
    config.endpointsDir = path.resolve(endpointsDir);
  }
  if (preferredProduct !== undefined) {
    config.preferredProduct = preferredProduct;
  }
  if (preferredBridge !== undefined) {
    config.preferredBridge = preferredBridge;
  }

  const reconnectDelayMs = readNumber(env.AUTODESK_MCP_RECONNECT_DELAY_MS, config.reconnectDelayMs);
  const maxReconnectAttempts = readNumber(env.AUTODESK_MCP_MAX_RECONNECT_ATTEMPTS, config.maxReconnectAttempts);
  const retryCooldownMs = readNumber(env.AUTODESK_MCP_RETRY_COOLDOWN_MS, config.retryCooldownMs);
  const requestTimeoutMs = readNumber(env.AUTODESK_MCP_REQUEST_TIMEOUT_MS, config.requestTimeoutMs);
  const heartbeatIntervalMs = readNumber(env.AUTODESK_MCP_HEARTBEAT_INTERVAL_MS, config.heartbeatIntervalMs);
  const endpointsPollIntervalMs = readNumber(env.AUTODESK_MCP_ENDPOINTS_POLL_INTERVAL_MS, config.endpointsPollIntervalMs);
  if (env.AUTODESK_MCP_RECONNECT_DELAY_MS !== undefined) {
    config.reconnectDelayMs = reconnectDelayMs;
  }
  if (env.AUTODESK_MCP_MAX_RECONNECT_ATTEMPTS !== undefined) {
    config.maxReconnectAttempts = maxReconnectAttempts;
  }
  if (env.AUTODESK_MCP_RETRY_COOLDOWN_MS !== undefined) {
    config.retryCooldownMs = retryCooldownMs;
  }
  if (env.AUTODESK_MCP_REQUEST_TIMEOUT_MS !== undefined) {
    config.requestTimeoutMs = requestTimeoutMs;
  }
  if (env.AUTODESK_MCP_HEARTBEAT_INTERVAL_MS !== undefined) {
    config.heartbeatIntervalMs = heartbeatIntervalMs;
  }
  if (env.AUTODESK_MCP_ENDPOINTS_POLL_INTERVAL_MS !== undefined) {
    config.endpointsPollIntervalMs = endpointsPollIntervalMs;
  }

  return config;
}

/** Extracts the endpoint preferences from a loaded configuration. */
export function toEndpointPreferences(config: ServerConfig): EndpointPreferences {
  return {
    preferredProduct: config.preferredProduct,
    preferredBridge: config.preferredBridge,
  };
}
