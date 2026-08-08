import { spawn, type ChildProcess } from 'node:child_process';

export interface RawMessage {
  jsonrpc?: string;
  id?: number;
  method?: string;
  params?: unknown;
  result?: unknown;
  error?: unknown;
}

/**
 * A deliberately minimal JSON-RPC 2.0 client that talks directly to the server's
 * stdout/stdin. Used by the E2E tests that need byte-level control over MCP
 * messages (cancellation, progress tokens, shutdown) without SDK abstractions.
 */
export class RawMcpClient {
  private readonly child: ChildProcess;
  private readonly pending = new Map<number, { resolve: (message: RawMessage) => void; reject: (error: Error) => void }>();
  private readonly listeners = new Set<(message: RawMessage) => void>();
  private readonly buffer: string[] = [];
  private nextId = 1;
  private closed = false;

  /** The ids of every request sent, in order (used to correlate cancellations). */
  readonly requestIds: number[] = [];

  private constructor(child: ChildProcess) {
    this.child = child;
    child.stdout?.setEncoding('utf8');
    child.stdout?.on('data', (chunk: string) => {
      this.buffer.push(chunk);
      let text = this.buffer.join('');
      this.buffer.length = 0;
      const lines = text.split(/\r?\n/);
      while (lines.length > 1) {
        const line = lines.shift();
        if (line !== undefined && line.trim().length > 0) {
          this.dispatch(line.trim());
        }
      }
      this.buffer.push(lines[0] ?? '');
    });
  }

  /** Spawns the server and performs the MCP initialize handshake. */
  static async connect(distIndex: string, env: Record<string, string>): Promise<RawMcpClient> {
    const child = spawn(process.execPath, [distIndex], {
      env: { ...process.env, ...env },
      stdio: ['pipe', 'pipe', 'pipe'],
    });
    const client = new RawMcpClient(child);
    const response = await client.request('initialize', {
      protocolVersion: '2025-06-18',
      capabilities: {},
      clientInfo: { name: 'autodesk-mcp-e2e-raw', version: '1.0.0' },
    });
    if (response.error !== undefined) {
      throw new Error(`initialize failed: ${JSON.stringify(response.error)}`);
    }
    client.notify('notifications/initialized', {});
    return client;
  }

  /** Sends a request and resolves with the matching response. */
  request(method: string, params: unknown): Promise<RawMessage> {
    if (this.closed) {
      return Promise.reject(new Error('The raw client is closed.'));
    }
    const id = this.nextId;
    this.nextId += 1;
    this.requestIds.push(id);
    return new Promise<RawMessage>((resolve, reject) => {
      this.pending.set(id, { resolve, reject });
      const timer = setTimeout(() => {
        this.pending.delete(id);
        reject(new Error(`Timed out waiting for response to '${method}' (id ${id}).`));
      }, 15_000);
      this.pending.get(id)!.reject = (error: Error) => {
        clearTimeout(timer);
        reject(error);
      };
      this.pending.get(id)!.resolve = (message: RawMessage) => {
        clearTimeout(timer);
        resolve(message);
      };
      this.write({ jsonrpc: '2.0', id, method, params });
    });
  }

  /** Sends a notification (no id, no reply). */
  notify(method: string, params?: unknown): void {
    this.write({ jsonrpc: '2.0', method, params });
  }

  /** Registers a listener for every inbound message (requests + notifications). */
  onMessage(listener: (message: RawMessage) => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  close(): void {
    this.closed = true;
    this.child.kill();
  }

  private write(message: RawMessage): void {
    if (this.child.stdin?.writable === true) {
      this.child.stdin.write(`${JSON.stringify(message)}\n`);
    }
  }

  private dispatch(line: string): void {
    let message: RawMessage;
    try {
      message = JSON.parse(line) as RawMessage;
    } catch {
      return;
    }
    if (typeof message.id === 'number') {
      const waiter = this.pending.get(message.id);
      if (waiter !== undefined) {
        this.pending.delete(message.id);
        if (message.error !== undefined) {
          waiter.reject(new Error(JSON.stringify(message.error)));
        } else {
          waiter.resolve(message);
        }
      }
    }
    for (const listener of [...this.listeners]) {
      listener(message);
    }
  }
}
