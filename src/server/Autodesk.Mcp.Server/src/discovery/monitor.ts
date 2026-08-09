import { EventEmitter } from 'node:events';
import { cleanupStaleEndpoints, scanEndpoints, type EndpointLogger } from './endpointStore.js';

export interface EndpointMonitorOptions {
  endpointsDir: string;
  /** Polling interval in milliseconds. */
  pollIntervalMs?: number;
  /** Clean stale (dead-pid) descriptor files on each poll. */
  cleanupStale?: boolean;
  logger?: EndpointLogger;
}

/**
 * Polls the endpoints registry and reports what it finds. Drives bridge discovery: bridges appear
 * when their descriptor file is written, disappear when it is removed, and stale files are cleaned
 * up on each poll.
 *
 * Two events are emitted because the consumer needs both views:
 * - 'snapshot' fires on every poll with the current endpoint set. The manager uses it to re-attempt
 *   a connection to an endpoint that is still registered but was not reachable earlier; a
 *   change-only event would strand the manager whenever a connection failed without the registry
 *   changing.
 * - 'update' fires only when the endpoint set changed, for logging and change-driven consumers.
 */
export class EndpointMonitor extends EventEmitter {
  private readonly options: Required<Pick<EndpointMonitorOptions, 'endpointsDir' | 'pollIntervalMs' | 'cleanupStale'>> &
    Pick<EndpointMonitorOptions, 'logger'>;
  private timer: NodeJS.Timeout | null = null;
  private fingerprint = '';

  constructor(options: EndpointMonitorOptions) {
    super();
    this.options = {
      endpointsDir: options.endpointsDir,
      pollIntervalMs: options.pollIntervalMs ?? 3_000,
      cleanupStale: options.cleanupStale ?? true,
      logger: options.logger,
    };
  }

  /** Starts polling. Idempotent. */
  start(): void {
    if (this.timer !== null) {
      return;
    }
    this.poll();
    this.timer = setInterval(() => this.poll(), this.options.pollIntervalMs);
  }

  /** Stops polling. */
  stop(): void {
    if (this.timer !== null) {
      clearInterval(this.timer);
      this.timer = null;
    }
  }

  private poll(): void {
    if (this.options.cleanupStale) {
      const removed = cleanupStaleEndpoints(this.options.endpointsDir, this.options.logger);
      if (removed > 0) {
        this.options.logger?.debug('Stale endpoint cleanup removed %d descriptor(s).', removed);
      }
    }

    const endpoints = scanEndpoints(this.options.endpointsDir, this.options.logger);
    const fingerprint = endpoints
      .map((endpoint) => `${endpoint.pipeName}:${endpoint.pid}`)
      .sort()
      .join('|');
    if (fingerprint !== this.fingerprint) {
      this.fingerprint = fingerprint;
      this.emit('update', endpoints);
    }
    this.emit('snapshot', endpoints);
  }
}
