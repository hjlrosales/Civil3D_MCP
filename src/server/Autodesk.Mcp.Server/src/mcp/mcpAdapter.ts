import type { Transport } from '@modelcontextprotocol/sdk/shared/transport.js';

import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
  McpError,
  PingRequestSchema,
  type CallToolRequest,
  type CallToolResult,
  type Tool,
} from '@modelcontextprotocol/sdk/types.js';
import { randomUUID } from 'node:crypto';
import { ErrorCode, type Manifest, type ProgressNotification, type ResponseEnvelope, type ToolManifest } from '../protocol/types.js';
import { bridgeFailureContent, McpErrorCodes, successContent } from './errors.js';
import { SchemaValidator, splitControlArgs } from './schema.js';
import type { BridgeClient } from '../bridge/bridgeClient.js';

export interface McpAdapterOptions {
  /** Server identity reported in the MCP initialize handshake. */
  serverName: string;
  serverVersion: string;
  /** Resolves the current bridge client; null when no bridge is connected. */
  getBridge: () => BridgeClient | null;
  logger: {
    info(message: string, ...args: any[]): void;
    warn(message: string, ...args: any[]): void;
    error(message: string, ...args: any[]): void;
    debug(message: string, ...args: any[]): void;
  };
}

/**
 * Exposes the bridge tool catalog to an MCP client through the official SDK Server. Tool
 * discovery is fully dynamic: every MCP tools/list reads the current bridge manifest, and every
 * tools/call validates arguments against the bridge input schema, forwards execution over the
 * pipe, maps the bridge envelope to an MCP result, and forwards progress and cancellation.
 */
export class McpAdapter {
  private readonly options: McpAdapterOptions;
  private readonly validator = new SchemaValidator();
  private readonly progressTokens = new Map<string, string | number>();
  private manifest: Manifest | null = null;
  private server: Server | null = null;
  /** Signature of the catalog last advertised to the client, for change detection. */
  private advertisedSignature = '';
  /** True once the MCP client has completed initialization (notifications are safe to send). */
  private initialized = false;

  constructor(options: McpAdapterOptions) {
    this.options = options;
  }

  /** The most recently loaded bridge manifest, or null. */
  get currentManifest(): Manifest | null {
    return this.manifest;
  }

  /**
   * Applies a freshly loaded (or changed) manifest. Clients such as VS Code call tools/list once,
   * immediately after initialize - long before a bridge is discovered - and never poll again, so
   * the catalog change has to be pushed to them or the session stays permanently empty.
   */
  updateManifest(manifest: Manifest): void {
    this.manifest = manifest;
    // Tool input schemas may have changed with the manifest; drop stale validators.
    this.validator.clear();
    this.publishToolListChange();
  }

  /**
   * Drops the advertised catalog because no bridge is available (Civil 3D closed, or the bridge
   * became unreachable). tools/list must not keep advertising tools that cannot run.
   */
  clearManifest(): void {
    if (this.manifest === null) {
      return;
    }
    this.manifest = null;
    this.validator.clear();
    this.publishToolListChange();
  }

  /**
   * Sends notifications/tools/list_changed when the visible catalog actually changed. The
   * signature covers the names and versions that tools/list exposes, so an identical manifest
   * reloaded after a reconnect does not churn the client.
   */
  private publishToolListChange(): void {
    const signature = this.catalogSignature();
    if (signature === this.advertisedSignature) {
      return;
    }
    this.advertisedSignature = signature;

    const server = this.server;
    if (server === null || !this.initialized) {
      // Not connected yet, or the client has not finished initializing. oninitialized replays
      // the notification once the session is ready.
      return;
    }

    const toolCount = this.manifest?.tools.length ?? 0;
    server.sendToolListChanged().then(
      () => this.options.logger.info('Advertised %d tool(s) to the MCP client (tools/list_changed sent).', toolCount),
      (error: unknown) => this.options.logger.warn(
        'Failed to notify the MCP client of the tool-list change: %s',
        error instanceof Error ? error.message : String(error),
      ),
    );
  }

  private catalogSignature(): string {
    const manifest = this.manifest;
    if (manifest === null) {
      return '';
    }
    return manifest.tools
      .filter((tool) => !tool.deprecated)
      .map((tool) => `${tool.name}@${tool.version}`)
      .sort()
      .join('|');
  }

  /** Registers MCP handlers on a fresh SDK Server and connects it to the transport. */
  async attach(transport: Transport): Promise<void> {
    if (this.server !== null) {
      throw new Error('The MCP adapter is already attached to a transport.');
    }

    const server = new Server(
      { name: this.options.serverName, version: this.options.serverVersion },
      // listChanged tells the client to subscribe to notifications/tools/list_changed. Without
      // it, a client that lists tools before the bridge is discovered never sees the catalog.
      { capabilities: { tools: { listChanged: true } } },
    );
    server.setRequestHandler(ListToolsRequestSchema, () => this.handleListTools());
    server.setRequestHandler(CallToolRequestSchema, (request, extra) => this.handleCallTool(request, extra));
    server.setRequestHandler(PingRequestSchema, async () => ({}));
    server.onerror = (error: unknown) => this.options.logger.error('MCP server error: %o', error);
    server.oninitialized = () => {
      this.initialized = true;
      // The bridge may have been discovered before the client finished initializing; replay the
      // catalog change now that notifications are deliverable.
      if (this.manifest !== null) {
        const toolCount = this.manifest.tools.length;
        server.sendToolListChanged().then(
          () => this.options.logger.info('Advertised %d tool(s) to the MCP client on initialize.', toolCount),
          () => undefined,
        );
      }
    };

    this.server = server;
    await server.connect(transport);
    this.options.logger.info('MCP adapter attached to transport.');
  }

  /** Detaches from the transport and clears in-flight state. */
  async close(): Promise<void> {
    const server = this.server;
    this.server = null;
    this.initialized = false;
    this.progressTokens.clear();
    if (server !== null) {
      await server.close();
    }
  }

  /** Serves tools/list from the current bridge manifest (dynamic discovery, never hardcoded). */
  private handleListTools(): { tools: Tool[] } {
    const manifest = this.manifest;
    if (manifest === null) {
      return { tools: [] };
    }

    return {
      tools: manifest.tools
        .filter((tool) => !tool.deprecated)
        .map((tool) => this.manifestToMcpTool(tool)),
    };
  }

  private manifestToMcpTool(tool: ToolManifest): Tool {
    return {
      name: tool.name,
      description: tool.description,
      inputSchema: tool.inputSchema as Tool['inputSchema'],
      annotations: {
        title: tool.displayName,
        readOnlyHint: tool.permission === 'ReadOnly',
        destructiveHint: tool.risk === 'High' || tool.risk === 'Critical',
      },
    };
  }

  /**
   * Executes one tools/call: validates arguments, forwards to the bridge, maps the envelope to
   * an MCP result. The SDK aborts extra.signal when the client sends notifications/cancelled;
   * that abort is forwarded to the bridge as $/cancel.
   */
  private async handleCallTool(request: CallToolRequest, extra: { signal: AbortSignal }): Promise<CallToolResult> {
    const toolName = request.params.name;
    const bridge = this.options.getBridge();
    if (bridge === null) {
      throw new McpError(McpErrorCodes.ServerError, 'No bridge is currently connected. Start Civil 3D with the bridge loaded and try again.');
    }

    const manifest = this.manifest?.tools.find((tool) => tool.name === toolName);
    const args = isRecord(request.params.arguments) ? request.params.arguments : {};
    const { toolArgs, confirm, progressToken } = splitControlArgs(args);

    const validationError = this.validator.validate(toolName, manifest?.inputSchema, toolArgs);
    if (validationError !== null) {
      throw new McpError(McpErrorCodes.InvalidParams, `Invalid arguments for '${toolName}': ${validationError}`);
    }

    const correlationId = randomUUID();
    if (progressToken !== undefined) {
      this.progressTokens.set(correlationId, progressToken);
    }
    const onAbort = (): void => {
      bridge.cancel(correlationId, 'Cancelled by the MCP client.');
      this.options.logger.info('Cancellation forwarded for correlation %s.', correlationId);
    };
    extra.signal.addEventListener('abort', onAbort);
    // A signal that was already aborted before we attached (e.g. the client cancelled during
    // validation) never re-emits; forward the cancellation immediately in that case.
    if (extra.signal.aborted) {
      onAbort();
    }

    const timer = performance.now();
    try {
      const envelope = await bridge.execute(toolName, toolArgs, {
        correlationId,
        timeoutMs: manifest?.timeoutMilliseconds,
        confirm,
      });
      return this.envelopeToResult(toolName, envelope, correlationId);
    } finally {
      extra.signal.removeEventListener('abort', onAbort);
      this.progressTokens.delete(correlationId);
      this.options.logger.debug(
        'Tool %s completed in %d ms (correlation %s).',
        toolName,
        Math.round(performance.now() - timer),
        correlationId,
      );
    }
  }

  private envelopeToResult(toolName: string, envelope: ResponseEnvelope, correlationId: string): CallToolResult {
    if (envelope.success) {
      this.options.logger.info('Tool %s succeeded (correlation %s).', toolName, correlationId);
      return { content: [successContent(envelope.data)] };
    }

    this.options.logger.warn(
      'Tool %s failed with %s: %s (correlation %s).',
      toolName,
      envelope.errorCode ?? ErrorCode.E_UNKNOWN,
      envelope.message,
      correlationId,
    );
    return { content: [bridgeFailureContent(envelope.errorCode, envelope.message, correlationId)], isError: true };
  }

  /** Forwards a bridge $/progress notification to the MCP client as notifications/progress. */
  handleBridgeProgress(progress: ProgressNotification): void {
    const server = this.server;
    if (server === null) {
      return;
    }
    const progressToken = this.progressTokens.get(progress.correlationId);
    if (progressToken === undefined) {
      return;
    }

    const message = [progress.stage, progress.message].filter(Boolean).join(' ');
    void server.notification({
      method: 'notifications/progress',
      params: {
        progressToken,
        progress: Math.max(0, Math.min(100, progress.percent)),
        total: 100,
        message: message.length > 0 ? message : undefined,
      },
    });
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
