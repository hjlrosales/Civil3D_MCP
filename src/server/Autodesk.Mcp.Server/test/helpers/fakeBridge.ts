import net from 'node:net';
import { randomUUID } from 'node:crypto';
import { pipePath } from '../../src/transport/pipe.js';
import { NdjsonSocket } from '../../src/transport/ndjson.js';
import { Notification } from '../../src/protocol/constants.js';
import type { Manifest, ResponseEnvelope, ToolManifest } from '../../src/protocol/types.js';

/** Builds a wire-faithful ResponseEnvelope (camelCase, correlation echoed, success true). */
export function okEnvelope(data: unknown, correlationId?: string, sessionId?: string): ResponseEnvelope {
  return {
    success: true,
    message: '',
    executionTime: 1,
    errorCode: 'E_UNKNOWN',
    correlationId,
    sessionId,
    data,
  };
}

/** Builds a wire-faithful failure ResponseEnvelope. */
export function failEnvelope(code: string, message: string, correlationId?: string, sessionId?: string): ResponseEnvelope {
  return {
    success: false,
    message,
    executionTime: 1,
    errorCode: code,
    correlationId,
    sessionId,
  };
}

/** A representative manifest used by most tests. */
export function sampleManifest(overrides: Partial<ToolManifest>[] = []): Manifest {
  const base: ToolManifest[] = [
    {
      name: 'drawing_info',
      displayName: 'Drawing Info',
      description: 'Returns information about the active drawing.',
      version: '1.0.0',
      permission: 'ReadOnly',
      risk: 'Low',
      timeoutMilliseconds: 30000,
      supportsProgress: false,
      supportsCancellation: false,
      inputSchema: { type: 'object', properties: {}, additionalProperties: false },
    },
    {
      name: 'echo',
      displayName: 'Echo',
      description: 'Echoes the supplied text.',
      version: '1.0.0',
      permission: 'ReadOnly',
      risk: 'Low',
      timeoutMilliseconds: 30000,
      supportsProgress: false,
      supportsCancellation: false,
      inputSchema: {
        type: 'object',
        properties: { text: { type: 'string' } },
        required: ['text'],
        additionalProperties: false,
      },
    },
    {
      name: 'rename_alignment',
      displayName: 'Rename Alignment',
      description: 'Renames an alignment (requires confirmation).',
      version: '1.0.0',
      permission: 'Modify',
      risk: 'High',
      timeoutMilliseconds: 30000,
      supportsProgress: false,
      supportsCancellation: false,
      inputSchema: {
        type: 'object',
        properties: { id: { type: 'integer' }, newName: { type: 'string' } },
        required: ['id', 'newName'],
        additionalProperties: false,
      },
    },
  ];
  for (const [index, override] of overrides.entries()) {
    if (override !== undefined && base[index] !== undefined) {
      base[index] = { ...base[index], ...override };
    }
  }
  return {
    schemaVersion: 1,
    protocolVersion: '1.0.0',
    generatedAtUtc: new Date().toISOString(),
    tools: base,
  };
}

export interface FakeBridgeOptions {
  pipeName: string;
  manifest?: Manifest;
  /** Custom execute behaviour; defaults to an echo of the tool name. */
  onExecute?: (tool: string, args: unknown, confirm?: boolean) => ResponseEnvelope | Promise<ResponseEnvelope>;
  /** When set, the fake delays every tools/execute by this many milliseconds. */
  executeDelayMs?: number;
}

interface ClientSession {
  socket: NdjsonSocket;
  sessionId: string;
}

/**
 * A protocol-faithful stand-in for the C# bridge: listens on a real Windows named pipe, speaks
 * the same NDJSON envelope wire format, and answers handshake / tools/list / tools/execute /
 * health/ping / shutdown / $/cancel exactly like the bridge's JsonRpcRouter.
 */
export class FakeBridge {
  readonly pipeName: string;
  private readonly manifest: Manifest;
  private readonly onExecute?: (tool: string, args: unknown, confirm?: boolean) => ResponseEnvelope | Promise<ResponseEnvelope>;
  private readonly executeDelayMs: number;
  private server: net.Server | null = null;
  private readonly clients = new Set<ClientSession>();
  private sessionCounter = 0;

  /** Every request envelope received, for assertions. */
  readonly requests: Array<{ method: string; correlationId?: string; params?: unknown }> = [];

  /** Correlation ids received via $/cancel. */
  readonly cancels: string[] = [];

  constructor(options: FakeBridgeOptions) {
    this.pipeName = options.pipeName;
    this.manifest = options.manifest ?? sampleManifest();
    this.onExecute = options.onExecute;
    this.executeDelayMs = options.executeDelayMs ?? 0;
  }

  get toolNames(): string[] {
    return this.manifest.tools.map((tool) => tool.name);
  }

  async start(): Promise<void> {
    this.server = net.createServer((socket) => this.onConnection(socket));
    await new Promise<void>((resolve, reject) => {
      this.server!.once('error', reject);
      // On Windows the listen path is the full named-pipe path; Node treats a string
      // path as a named pipe and creates the kernel pipe object when listening.
      this.server!.listen(pipePath(this.pipeName), () => resolve());
    });
  }

  async stop(): Promise<void> {
    for (const client of this.clients) {
      client.socket.destroy();
    }
    this.clients.clear();
    if (this.server !== null) {
      const server = this.server;
      this.server = null;
      // Force-close any connections that survived a failed test body; otherwise close()
      // waits for them and the test hook times out. (closeAllConnections is Node 18.2+;
      // the runtime has it even though the bundled @types/node may not.)
      (server as unknown as { closeAllConnections?: () => void }).closeAllConnections?.();
      await new Promise<void>((resolve) => server.close(() => resolve()));
    }
  }

  /** Simulates an abrupt bridge crash: destroys every client connection with an error. */
  abortAllConnections(): void {
    for (const client of this.clients) {
      client.socket.destroy(new Error('simulated bridge crash'));
    }
  }

  /** Streams a $/progress notification to the most recent client connection. */
  sendProgress(correlationId: string, percent: number, stage: string, message?: string): void {
    const latest = [...this.clients].at(-1);
    if (latest === undefined) {
      throw new Error('No client connected to the fake bridge.');
    }
    latest.socket.send({
      method: Notification.Progress,
      params: {
        correlationId,
        sessionId: latest.sessionId,
        toolName: 'test.tool',
        percent,
        stage,
        message,
        timestampUtc: new Date().toISOString(),
      },
    });
  }

  private onConnection(socket: net.Socket): void {
    const ndjson = new NdjsonSocket(socket);
    // Sessions are tracked from the moment the pipe connects (not only after the handshake),
    // so the raw BridgeConnection tests can stream progress notifications too.
    const client: ClientSession = { socket: ndjson, sessionId: `sess-${++this.sessionCounter}` };
    this.clients.add(client);
    ndjson.on('message', (message: unknown) => void this.onMessage(ndjson, client, message));
    ndjson.on('error', () => undefined);
    ndjson.on('close', () => this.clients.delete(client));
  }

  private async onMessage(socket: NdjsonSocket, client: ClientSession, raw: unknown): Promise<void> {
    const message = raw as { method?: string; correlationId?: string; sessionId?: string; params?: unknown };
    if (typeof message.method !== 'string') {
      return;
    }
    this.requests.push({ method: message.method, correlationId: message.correlationId, params: message.params });

    const correlationId = message.correlationId;
    switch (message.method) {
      case 'handshake': {
        socket.send(okEnvelope({
          protocolVersion: '1.0.0',
          sessionId: client.sessionId,
          bridge: {
            bridgeName: 'Civil3D.Bridge',
            product: 'Civil3D',
            productVersion: '2026',
            bridgeVersion: '1.0.0',
            sdkVersion: '1.0.0',
            protocolVersion: '1.0.0',
          },
        }, correlationId));
        break;
      }
      case 'tools/list':
        socket.send(okEnvelope(this.manifest, correlationId));
        break;
      case 'health/ping':
        socket.send(okEnvelope({ timestampUtc: new Date().toISOString(), sessionId: message.sessionId, processId: 0 }, correlationId));
        break;
      case 'shutdown':
        socket.send(okEnvelope('Shutdown requested.', correlationId));
        setTimeout(() => socket.destroy(), 10);
        break;
      case 'tools/execute': {
        const params = message.params as { tool?: string; arguments?: unknown; confirm?: boolean };
        if (this.executeDelayMs > 0) {
          await new Promise((resolve) => setTimeout(resolve, this.executeDelayMs));
        }
        const envelope = this.onExecute !== undefined
          ? await this.onExecute(params.tool ?? '', params.arguments, params.confirm)
          : okEnvelope({ tool: params.tool, echoed: params.arguments }, correlationId, message.sessionId);
        socket.send({ ...envelope, correlationId, sessionId: message.sessionId });
        break;
      }
      case '$/cancel': {
        const params = message.params as { correlationId?: string };
        if (typeof params?.correlationId === 'string') {
          this.cancels.push(params.correlationId);
        }
        break;
      }
      default:
        socket.send(failEnvelope('E_INVALID_REQUEST', `Unknown method '${message.method}'.`, correlationId));
        break;
    }
  }
}

/** Creates a unique pipe name for a test instance. */
export function uniquePipeName(prefix = 'autodesk-mcp-test'): string {
  return `${prefix}-${randomUUID().replaceAll('-', '')}`;
}
