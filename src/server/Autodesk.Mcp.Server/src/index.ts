#!/usr/bin/env node
import { createRequire } from 'node:module';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { BridgeManager } from './manager.js';
import { McpAdapter } from './mcp/mcpAdapter.js';
import { loadConfig, toEndpointPreferences } from './config.js';
import { createLogger } from './logger.js';
import { CurrentProtocolVersion } from './protocol/constants.js';

const require = createRequire(import.meta.url);
const packageInfo = require('../package.json') as { name: string; version: string };

interface CliOptions {
  configPath?: string;
}

function printUsage(): void {
  process.stderr.write(
    [
      `${packageInfo.name} ${packageInfo.version}`,
      '',
      'Usage: autodesk-mcp-server [options]',
      '',
      'Options:',
      '  -c, --config <path>   Path to a JSON configuration file (or $AUTODESK_MCP_CONFIG).',
      '  -V, --version         Print the server version and exit.',
      '  -h, --help            Print this help and exit.',
      '',
      'Runs an MCP server over stdio. Bridges are discovered from the endpoint registry',
      '(default %LOCALAPPDATA%\\AutodeskMcp\\endpoints).',
      '',
    ].join('\n'),
  );
}

function parseArgv(argv: string[]): CliOptions {
  const options: CliOptions = {};
  for (let i = 0; i < argv.length; i += 1) {
    const arg = argv[i];
    if (arg === '--version' || arg === '-V') {
      process.stdout.write(`${packageInfo.version}\n`);
      process.exit(0);
    }
    if (arg === '--help' || arg === '-h') {
      printUsage();
      process.exit(0);
    }
    if ((arg === '--config' || arg === '-c') && argv[i + 1] !== undefined) {
      options.configPath = argv[i + 1];
    }
  }
  return options;
}

async function main(): Promise<void> {
  const { configPath } = parseArgv(process.argv.slice(2));
  const config = loadConfig(configPath ?? process.env.AUTODESK_MCP_CONFIG);
  const logger = createLogger(config.logLevel);
  logger.info('Autodesk MCP Server %s starting (protocol %s, client %s %s).', packageInfo.version, CurrentProtocolVersion, config.clientName, config.clientVersion);
  logger.info('Monitoring endpoints in %s.', config.endpointsDir);

  const manager = new BridgeManager({
    endpointsDir: config.endpointsDir,
    preferences: toEndpointPreferences(config),
    clientName: config.clientName,
    clientVersion: config.clientVersion,
    capabilities: { supportsConfirmation: true, supportsProgress: true, supportsCancellation: true },
    reconnectDelayMs: config.reconnectDelayMs,
    maxReconnectAttempts: config.maxReconnectAttempts,
    retryCooldownMs: config.retryCooldownMs,
    requestTimeoutMs: config.requestTimeoutMs,
    heartbeatIntervalMs: config.heartbeatIntervalMs,
    endpointsPollIntervalMs: config.endpointsPollIntervalMs,
    logger: {
      info: (message: string, ...args: any[]) => logger.info(message, ...args),
      warn: (message: string, ...args: any[]) => logger.warn(message, ...args),
      debug: (message: string, ...args: any[]) => logger.debug(message, ...args),
    },
  });

  const adapter = new McpAdapter({
    serverName: packageInfo.name,
    serverVersion: packageInfo.version,
    getBridge: () => manager.getBridge(),
    logger: {
      info: (message: string, ...args: any[]) => logger.info(message, ...args),
      warn: (message: string, ...args: any[]) => logger.warn(message, ...args),
      error: (message: string, ...args: any[]) => logger.error(message, ...args),
      debug: (message: string, ...args: any[]) => logger.debug(message, ...args),
    },
  });

  manager.on('manifest', (manifest: Parameters<McpAdapter['updateManifest']>[0]) => {
    adapter.updateManifest(manifest);
  });
  // No bridge is available any more: stop advertising tools that cannot run.
  manager.on('manifestCleared', () => {
    adapter.clearManifest();
    logger.info('No bridge is connected; 0 tools are available until Civil 3D returns.');
  });
  manager.on('progress', (progress: Parameters<McpAdapter['handleBridgeProgress']>[0]) => {
    adapter.handleBridgeProgress(progress);
  });
  manager.on('status', (status: string) => logger.info('Bridge status changed to %s.', status));

  await adapter.attach(new StdioServerTransport());
  manager.start();
  logger.info(
    'Autodesk MCP Server ready on stdio. Tools appear automatically once a Civil 3D bridge is discovered; the server keeps watching if Civil 3D is not running yet.',
  );

  const shutdown = async (signal: string): Promise<void> => {
    logger.info('Received %s; shutting down.', signal);
    manager.stop();
    await adapter.close();
    process.exit(0);
  };
  process.on('SIGINT', () => void shutdown('SIGINT'));
  process.on('SIGTERM', () => void shutdown('SIGTERM'));
}

main().catch((error: unknown) => {
  const message = error instanceof Error ? error.stack ?? error.message : String(error);
  process.stderr.write(`autodesk-mcp-server: fatal startup error\n${message}\n`);
  process.exit(1);
});
