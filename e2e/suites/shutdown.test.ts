import { afterEach, describe, expect, it } from 'vitest';
import { startHarness, stopHarness, waitForTools, type Harness } from '../helpers/harness.js';

describe('server shutdown end to end', () => {
  let harness: Harness | null = null;

  afterEach(async () => {
    if (harness !== null) {
      await stopHarness(harness);
      harness = null;
    }
  });

  it('terminates the server process on SIGTERM without hanging', async () => {
    harness = await startHarness({ fast: true });
    await waitForTools(harness.client);

    const child = harness.child;
    const exited = new Promise<number | null>((resolve) => {
      child.once('exit', (code) => resolve(code));
    });

    child.kill('SIGTERM');

    const code = await exited;
    // Windows maps SIGTERM to a forced process termination; either way the process must not hang.
    expect(code).toBeDefined();
  });

  it('survives a client disconnect and keeps serving until told to stop', async () => {
    harness = await startHarness({ fast: true });
    await waitForTools(harness.client);

    // Close the MCP transport abruptly (client-side) - the server must keep running.
    await harness.transport.close();
    await new Promise((resolve) => setTimeout(resolve, 300));
    expect(harness.child.exitCode).toBeNull();
  });
});
