import { afterEach, describe, expect, it } from 'vitest';
import { FakeBridge, failEnvelope, uniquePipeName } from '../../src/server/Autodesk.Mcp.Server/test/helpers/fakeBridge.js';
import { startHarness, stopHarness, waitForTools, type Harness } from '../helpers/harness.js';

describe('tool execution end to end', () => {
  let harness: Harness | null = null;

  afterEach(async () => {
    if (harness !== null) {
      await stopHarness(harness);
      harness = null;
    }
  });

  it('executes a tool and returns the bridge payload', async () => {
    harness = await startHarness({ fast: true });
    await waitForTools(harness.client);

    const result = await harness.client.callTool({
      name: 'echo',
      arguments: { text: 'hello e2e' },
    });

    expect(result.isError).toBeFalsy();
    const text = result.content.map((part) => ('text' in part ? part.text : '')).join('');
    expect(text).toContain('hello e2e');
  });

  it('rejects invalid arguments with an invalid-params error', async () => {
    harness = await startHarness({ fast: true });
    await waitForTools(harness.client);

    await expect(
      harness.client.callTool({ name: 'echo', arguments: { nope: 42 } }),
    ).rejects.toThrow();
  });

  it('maps bridge business failures to structured isError results with the code preserved', async () => {
    const bridge = new FakeBridge({
      pipeName: uniquePipeName('autodesk-mcp-e2e-fail'),
      onExecute: () => failEnvelope('E_OBJECT_NOT_FOUND', 'Alignment 99 was not found.'),
    });
    await bridge.start();
    harness = await startHarness({ fast: true, bridge });
    await waitForTools(harness.client);

    const result = await harness.client.callTool({
      name: 'echo',
      arguments: { text: 'x' },
    });

    expect(result.isError).toBe(true);
    const text = result.content.map((part) => ('text' in part ? part.text : '')).join('');
    expect(text).toContain('E_OBJECT_NOT_FOUND');
    expect(text).toContain('Alignment 99 was not found.');
  });
});
