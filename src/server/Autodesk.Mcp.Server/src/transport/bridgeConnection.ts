import { EventEmitter } from 'node:events';
import { randomUUID } from 'node:crypto';
import { ErrorCode, type JsonRpcId, type RequestEnvelope, type ResponseEnvelope, type ErrorEnvelope } from '../protocol/types.js';
import { Notification } from '../protocol/constants.js';
import { BridgeProtocolError, NdjsonSocket } from './ndjson.js';
import { connectPipe } from './pipe.js';

/** A transport-, protocol- or timeout-level failure; never a bridge business error. */
export class BridgeConnectionError extends Error {
  constructor(
    message: string,
    /** A stable ErrorCode member name (E_TIMEOUT, E_BRIDGE_UNAVAILABLE, ...). */
    public readonly code: string,
    public readonly correlationId?: string,
  ) {
    super(message);
    this.name = 'BridgeConnectionError';
  }
}

interface PendingRequest {
  id: JsonRpcId;
  correlationId: string;
  resolve: (response: ResponseEnvelope) => void;
  reject: (error: Error) => void;
  timer: NodeJS.Timeout;
}

/** A notification received from the bridge (for example $/progress). */
export interface IncomingNotification {
  method: string;
  params?: unknown;
}

export interface BridgeConnectionOptions {
  pipeName: string;
  /** Timeout for establishing the pipe connection. */
  connectTimeoutMs?: number;
  /** Default per-request timeout applied when a request has no explicit override. */
  requestTimeoutMs?: number;
}

/**
 * A single named-pipe session to a bridge. Correlates responses to requests by correlation id
 * (the response envelope carries no JSON-RPC id), dispatches unsolicited notifications, applies
 * per-request timeouts, and rejects every in-flight request when the connection drops so the
 * manager can reconnect cleanly. One instance per connection; never reused after close.
 */
export class BridgeConnection extends EventEmitter {
  private socket: NdjsonSocket | null = null;
  private readonly pending = new Map<string, PendingRequest>();
  private nextId = 1;
  private readonly options: Required<Pick<BridgeConnectionOptions, 'pipeName'>> &
    Pick<BridgeConnectionOptions, 'connectTimeoutMs' | 'requestTimeoutMs'>;

  constructor(options: BridgeConnectionOptions) {
    super();
    this.options = {
      pipeName: options.pipeName,
      connectTimeoutMs: options.connectTimeoutMs ?? 10_000,
      requestTimeoutMs: options.requestTimeoutMs ?? 30_000,
    };
  }

  /** The pipe this connection talks to. */
  get pipeName(): string {
    return this.options.pipeName;
  }

  /** True while the underlying socket is connected and writable. */
  get connected(): boolean {
    return this.socket !== null && this.socket.writable;
  }

  /** Establishes the pipe connection and starts reading messages. */
  async connect(): Promise<void> {
    if (this.socket !== null) {
      throw new BridgeConnectionError('The connection is already established.', ErrorCode.E_BRIDGE_UNAVAILABLE);
    }

    const socket = await connectPipe(this.options.pipeName, this.options.connectTimeoutMs);
    const ndjson = new NdjsonSocket(socket);
    ndjson.on('message', (message: unknown) => this.onMessage(message));
    ndjson.on('error', (error: Error) => this.onSocketError(error));
    ndjson.on('close', () => this.onClose());
    this.socket = ndjson;
    this.emit('connected');
  }

  /**
   * Sends a JSON-RPC request and awaits the response envelope. Bridge business failures are
   * returned (response.success false, response.errorCode set); transport failures throw a
   * {@link BridgeConnectionError}. Correlation is by a fresh correlation id echoed by the bridge.
   */
  request(
    method: string,
    params?: unknown,
    options?: { correlationId?: string; sessionId?: string; timeoutMs?: number },
  ): Promise<ResponseEnvelope> {
    const socket = this.requireSocket();
    const correlationId = options?.correlationId ?? randomUUID();
    const timeoutMs = options?.timeoutMs ?? this.options.requestTimeoutMs ?? 30_000;

    const envelope: RequestEnvelope = {
      jsonrpc: '2.0',
      method,
      id: this.nextId,
      correlationId,
      sessionId: options?.sessionId,
      clientRequestedAtUtc: new Date().toISOString(),
    };
    this.nextId += 1;
    if (params !== undefined) {
      envelope.params = params;
    }

    return new Promise<ResponseEnvelope>((resolve, reject) => {
      const timer = setTimeout(() => {
        this.pending.delete(correlationId);
        reject(new BridgeConnectionError(`Request '${method}' timed out after ${timeoutMs} ms.`, ErrorCode.E_TIMEOUT, correlationId));
      }, timeoutMs);

      this.pending.set(correlationId, { id: envelope.id as number, correlationId, resolve, reject, timer });
      try {
        socket.send(envelope);
      } catch (error) {
        clearTimeout(timer);
        this.pending.delete(correlationId);
        reject(
          error instanceof BridgeProtocolError
            ? new BridgeConnectionError(error.message, ErrorCode.E_INVALID_REQUEST, correlationId)
            : new BridgeConnectionError('Failed to write to the bridge pipe.', ErrorCode.E_BRIDGE_UNAVAILABLE, correlationId),
        );
      }
    });
  }

  /** Sends a JSON-RPC notification (no reply expected). */
  notify(method: string, params?: unknown): void {
    const socket = this.requireSocket();
    const envelope: RequestEnvelope = { jsonrpc: '2.0', method };
    if (params !== undefined) {
      envelope.params = params;
    }
    socket.send(envelope);
  }

  /** Requests cancellation of an in-flight operation via the $/cancel notification. */
  cancel(correlationId: string, reason?: string): void {
    this.notify(Notification.Cancel, {
      correlationId,
      reason,
      requestedAtUtc: new Date().toISOString(),
    });
  }

  /** Performs a liveness check (health/ping) and returns the heartbeat data. */
  async ping(sessionId?: string): Promise<unknown> {
    const response = await this.request('health/ping', undefined, { sessionId, timeoutMs: 5_000 });
    if (!response.success) {
      throw new BridgeConnectionError(response.message || 'Bridge health check failed.', response.errorCode ?? ErrorCode.E_INTERNAL);
    }
    return response.data;
  }

  /** Requests a clean bridge shutdown (best-effort). */
  async shutdown(sessionId?: string): Promise<void> {
    try {
      await this.request('shutdown', undefined, { sessionId, timeoutMs: 5_000 });
    } catch {
      // Best-effort; the bridge stops shortly after and the connection closes.
    }
  }

  /** Closes the connection and rejects every in-flight request as unavailable. */
  close(): void {
    const socket = this.socket;
    this.socket = null;
    this.flushPending(new BridgeConnectionError('The bridge connection was closed.', ErrorCode.E_BRIDGE_UNAVAILABLE));
    if (socket !== null) {
      socket.destroy();
    }
  }

  private onMessage(message: unknown): void {
    if (!isRecord(message)) {
      this.onSocketError(new BridgeProtocolError('Received a wire message that is not an object.'));
      return;
    }

    // Notifications carry a method and no id; they never produce a response.
    if (typeof message.method === 'string' && !('id' in message)) {
      this.emit('notification', { method: message.method, params: message.params } satisfies IncomingNotification);
      return;
    }

    // Bridge business responses are ResponseEnvelopes; protocol errors are ErrorEnvelopes.
    if (typeof message.success === 'boolean') {
      const response = message as unknown as ResponseEnvelope;
      if (response.correlationId !== undefined && this.pending.has(response.correlationId)) {
        this.settle(response.correlationId, response);
      } else {
        this.emit('unmatched', response);
      }
      return;
    }

    if (typeof message.errorCode === 'string') {
      const error = message as unknown as ErrorEnvelope;
      if (error.correlationId !== undefined && this.pending.has(error.correlationId)) {
        this.settleReject(
          error.correlationId,
          new BridgeConnectionError(error.message || 'The bridge reported a protocol error.', error.errorCode, error.correlationId),
        );
      } else {
        this.emit('unmatched', error);
      }
      return;
    }

    this.onSocketError(new BridgeProtocolError('Received a wire message of unknown shape.'));
  }

  private settle(correlationId: string, response: ResponseEnvelope): void {
    const pending = this.pending.get(correlationId);
    if (pending === undefined) {
      return;
    }
    clearTimeout(pending.timer);
    this.pending.delete(correlationId);
    pending.resolve(response);
  }

  private settleReject(correlationId: string, error: Error): void {
    const pending = this.pending.get(correlationId);
    if (pending === undefined) {
      return;
    }
    clearTimeout(pending.timer);
    this.pending.delete(correlationId);
    pending.reject(error);
  }

  private flushPending(error: BridgeConnectionError): void {
    for (const pending of this.pending.values()) {
      clearTimeout(pending.timer);
      pending.reject(error);
    }
    this.pending.clear();
  }

  private onSocketError(error: Error): void {
    this.emit('error', error);
  }

  private onClose(): void {
    const hadSocket = this.socket !== null;
    this.socket = null;
    if (hadSocket) {
      this.flushPending(new BridgeConnectionError('The bridge connection was lost.', ErrorCode.E_BRIDGE_UNAVAILABLE));
    }
    this.emit('close');
  }

  private requireSocket(): NdjsonSocket {
    const socket = this.socket;
    if (socket === null || !socket.writable) {
      throw new BridgeConnectionError('The bridge is not connected.', ErrorCode.E_BRIDGE_UNAVAILABLE);
    }
    return socket;
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
