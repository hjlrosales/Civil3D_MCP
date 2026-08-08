import { afterEach, describe, expect, it } from 'vitest';
import { FakeBridge, failEnvelope, okEnvelope, uniquePipeName } from '../../src/server/Autodesk.Mcp.Server/test/helpers/fakeBridge.js';
import { startHarness, stopHarness, waitForTools, type Harness } from '../helpers/harness.js';

describe('confirmation flow end to end', () => {
  let harness: Harness | null = null;

  afterEach(async () => {
    if (harness !== null) {
      await stopHarness(harness);
      harness = null;
    }
  });

  it('returns confirmation-required with retry guidance, then succeeds with confirm: true', async () => {
    const bridge = new FakeBridge({
      pipeName: uniquePipeName('autodesk-mcp-e2e-confirm'),
      onExecute: (tool, args, confirm) =>
        confirm === true
          ? okEnvelope({ renamed: true, tool })
          : failEnvelope('E_CONFIRMATION_REQUIRED', 'Confirm the rename before proceeding.'),
    });
    await bridge.start();
    harness = await startHarness({ fast: true, bridge });
    await waitForTools(harness.client);

    const first = await harness.client.callTool({
      name: 'rename_alignment',
      arguments: { id: 1, newName: 'Road A' },
    });
    expect(first.isError).toBe(true);
    const text = first.content.map((part) => ('text' in part ? part.text : '')).join('');
    const parsed = JSON.parse(text);
    expect(parsed.code).toBe('E_CONFIRMATION_REQUIRED');
    expect(parsed.confirmation).toMatchObject({ retryWith: { confirm: true } });

    const second = await harness.client.callTool({
      name: 'rename_alignment',
      arguments: { id: 1, newName: 'Road A', confirm: true },
    });
    expect(second.isError).toBeFalsy();
    const okText = second.content.map((part) => ('text' in part ? part.text : '')).join('');
    expect(JSON.parse(okText)).toMatchObject({ renamed: true });
  });
});
