import { EventEmitter } from 'node:events';
import type { Duplex } from 'node:stream';
import { MaxMessageLength } from '../protocol/constants.js';

/** A wire-level framing or transport violation. */
export class BridgeProtocolError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'BridgeProtocolError';
  }
}

/**
 * Newline-delimited JSON framing (NDJSON) over an arbitrary duplex stream (a named-pipe socket
 * or a TCP socket): one UTF-8 JSON object per line, JSON never contains raw newlines, so framing
 * is unambiguous. Emits parsed 'message' objects; guards against oversized lines.
 */
export class NdjsonSocket extends EventEmitter {
  private buffer = '';
  private readonly socket: Duplex;

  constructor(socket: Duplex) {
    super();
    this.socket = socket;
    socket.on('data', (chunk: Buffer | string) => this.onData(chunk));
    socket.on('error', (error: Error) => this.emit('error', error));
    socket.on('end', () => this.emit('end'));
    socket.on('close', () => this.emit('close'));
  }

  private onData(chunk: Buffer | string): void {
    this.buffer += chunk.toString('utf8');
    let newlineIndex = this.buffer.indexOf('\n');
    while (newlineIndex >= 0) {
      const line = this.buffer.slice(0, newlineIndex);
      this.buffer = this.buffer.slice(newlineIndex + 1);
      if (line.endsWith('\r')) {
        // Tolerate CRLF line endings on read (the C# writer emits \n only).
        this.handleLine(line.slice(0, -1));
      } else {
        this.handleLine(line);
      }
      newlineIndex = this.buffer.indexOf('\n');
    }

    if (this.buffer.length > MaxMessageLength) {
      this.destroy(new BridgeProtocolError('A wire message exceeded the maximum allowed length.'));
    }
  }

  private handleLine(line: string): void {
    if (line.trim().length === 0) {
      return;
    }
    if (line.length > MaxMessageLength) {
      this.destroy(new BridgeProtocolError('A wire message exceeded the maximum allowed length.'));
      return;
    }

    let parsed: unknown;
    try {
      parsed = JSON.parse(line);
    } catch {
      this.destroy(new BridgeProtocolError('Received a wire message that is not valid JSON.'));
      return;
    }
    this.emit('message', parsed);
  }

  /** Serializes a payload and writes it as one JSON line. */
  send(payload: unknown): void {
    const json = JSON.stringify(payload);
    if (json === undefined) {
      throw new BridgeProtocolError('The payload cannot be serialized to JSON.');
    }
    if (json.length > MaxMessageLength) {
      throw new BridgeProtocolError('A wire message exceeded the maximum allowed length.');
    }
    this.socket.write(json + '\n');
  }

  /** True while the underlying stream is writable. */
  get writable(): boolean {
    return this.socket.writable && !this.socket.destroyed;
  }

  /** Terminates the connection. */
  destroy(error?: Error): void {
    this.socket.destroy(error);
  }
}
