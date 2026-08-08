import { afterEach, describe, expect, it } from 'vitest';
import { startHarness, stopHarness, waitForTools, type Harness } from '../helpers/harness.js';

describe('server startup, handshake and discovery', () => {
  let harness: Harness | null = null;

  afterEach(async () => {
    if (harness !== null) {
      await stopHarness(harness);
      harness = null;
    }
  });

  it('starts, completes the MCP handshake and discovers every bridge tool', async () => {
    harness = await startHarness({ fast: true });
    await waitForTools(harness.client);

    const result = await harness.client.listTools();
    const names = result.tools.map((tool) => tool.name);
    expect(names).toContain('drawing_info');
    expect(names).toContain('echo');
    expect(names).toContain('rename_alignment');
  });

  it('exposes the bridge input schema and annotations on each tool', async () => {
    harness = await startHarness({ fast: true });
    await waitForTools(harness.client);

    const result = await harness.client.listTools();
    const echo = result.tools.find((tool) => tool.name === 'echo');
    expect(echo).toBeDefined();
    expect(echo?.inputSchema).toMatchObject({
      type: 'object',
      properties: { text: { type: 'string' } },
      required: ['text'],
    });
    expect(echo?.annotations?.title).toBe('Echo');
    expect(echo?.annotations?.readOnlyHint).toBe(true);

    const rename = result.tools.find((tool) => tool.name === 'rename_alignment');
    expect(rename?.annotations?.destructiveHint).toBe(true);
  });
});
