import net from 'node:net';

/** A single backslash, built without any escape-sequence in source. */
const BS = String.fromCharCode(92);

/**
 * Builds the full Windows named-pipe path for a platform pipe name
 * (e.g. autodesk-mcp-civil3d-12345 becomes .pipe.autodesk-mcp-civil3d-12345 with the
 * backslash prefix \.\pipe\ applied at runtime).
 */
export function pipePath(pipeName: string): string {
  return BS + BS + '.' + BS + 'pipe' + BS + pipeName;
}

/**
 * Connects to a Windows named pipe. Node's net module treats the path option as a named-pipe
 * path on Windows, which is exactly the transport the C# bridge listens on.
 */
export function connectPipe(pipeName: string, timeoutMs = 10_000): Promise<net.Socket> {
  return new Promise<net.Socket>((resolve, reject) => {
    const socket = net.createConnection({ path: pipePath(pipeName) });
    let settled = false;

    const timer = setTimeout(() => {
      if (settled) {
        return;
      }
      settled = true;
      socket.destroy();
      reject(new Error(`Timed out connecting to pipe '${pipeName}' after ${timeoutMs} ms.`));
    }, timeoutMs);

    socket.once('connect', () => {
      if (settled) {
        return;
      }
      settled = true;
      clearTimeout(timer);
      resolve(socket);
    });

    socket.once('error', (error: Error) => {
      if (settled) {
        return;
      }
      settled = true;
      clearTimeout(timer);
      reject(error);
    });
  });
}
