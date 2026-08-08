import pino from 'pino';

/**
 * Creates the pino logger. All log output goes to stderr: the MCP server speaks JSON-RPC over
 * stdio, so stdout must stay clean for protocol traffic.
 */
export function createLogger(level: string): pino.Logger {
  return pino(
    {
      level: levelValid(level) ? level : 'info',
      base: { component: 'autodesk-mcp-server' },
      timestamp: pino.stdTimeFunctions.isoTime,
    },
    process.stderr,
  );
}

function levelValid(level: string): boolean {
  return ['trace', 'debug', 'info', 'warn', 'error', 'fatal'].includes(level);
}
