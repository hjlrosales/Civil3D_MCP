import { describe, expect, it } from 'vitest';
import { PassThrough } from 'node:stream';
import { BridgeProtocolError, NdjsonSocket } from '../src/transport/ndjson.js';

describe('NDJSON framing', () => {
  it('round-trips one JSON object per line', async () => {
    const duplex = new PassThrough();
    const ndjson = new NdjsonSocket(duplex);
    const messages: unknown[] = [];
    ndjson.on('message', (message: unknown) => messages.push(message));

    ndjson.send({ a: 1 });
    duplex.end();
    await new Promise((resolve) => setTimeout(resolve, 10));
    expect(messages).toEqual([{ a: 1 }]);
  });

  it('reads multiple lines in order', async () => {
    const duplex = new PassThrough();
    const ndjson = new NdjsonSocket(duplex);
    const messages: unknown[] = [];
    ndjson.on('message', (message: unknown) => messages.push(message));

    ndjson.send({ id: 1 });
    ndjson.send({ id: 2 });
    duplex.end();
    await new Promise((resolve) => setTimeout(resolve, 10));
    expect(messages).toEqual([{ id: 1 }, { id: 2 }]);
  });

  it('reassembles lines split across chunks', async () => {
    const duplex = new PassThrough();
    const ndjson = new NdjsonSocket(duplex);
    const messages: unknown[] = [];
    ndjson.on('message', (message: unknown) => messages.push(message));

    // Write one JSON line byte-by-byte across several writes.
    const line = Buffer.from('{"text":"hello world"}\n', 'utf8');
    for (let i = 0; i < line.length; i += 1) {
      duplex.write(line.subarray(i, i + 1));
    }
    await new Promise((resolve) => setTimeout(resolve, 10));
    expect(messages).toEqual([{ text: 'hello world' }]);
  });

  it('tolerates CRLF line endings', async () => {
    const duplex = new PassThrough();
    const ndjson = new NdjsonSocket(duplex);
    const messages: unknown[] = [];
    ndjson.on('message', (message: unknown) => messages.push(message));

    duplex.write('{"a":1}\r\n');
    duplex.end();
    await new Promise((resolve) => setTimeout(resolve, 10));
    expect(messages).toEqual([{ a: 1 }]);
  });

  it('fails on malformed JSON', async () => {
    const duplex = new PassThrough();
    const ndjson = new NdjsonSocket(duplex);
    const errors: Error[] = [];
    ndjson.on('error', (error: Error) => errors.push(error));

    duplex.write('this is not json\n');
    await new Promise((resolve) => setTimeout(resolve, 10));
    expect(errors[0]).toBeInstanceOf(BridgeProtocolError);
  });

  it('rejects oversized messages', async () => {
    const duplex = new PassThrough();
    const ndjson = new NdjsonSocket(duplex);
    const errors: Error[] = [];
    ndjson.on('error', (error: Error) => errors.push(error));

    expect(() => ndjson.send('x'.repeat(4 * 1024 * 1024 + 1))).toThrow(BridgeProtocolError);
    expect(() => duplex.end()).not.toThrow();
  });
});
