import { describe, expect, it } from 'vitest';
import { SchemaValidator, splitControlArgs } from '../src/mcp/schema.js';

const ECHO_SCHEMA = {
  type: 'object',
  properties: {
    text: { type: 'string' },
    count: { type: 'integer' },
  },
  required: ['text'],
  additionalProperties: false,
};

describe('schema validation (ajv)', () => {
  it('accepts valid arguments', () => {
    const validator = new SchemaValidator();
    expect(validator.validate('echo', ECHO_SCHEMA, { text: 'hi' })).toBeNull();
    expect(validator.validate('echo', ECHO_SCHEMA, { text: 'hi', count: 2 })).toBeNull();
  });

  it('rejects missing required properties with a readable message', () => {
    const validator = new SchemaValidator();
    const message = validator.validate('echo', ECHO_SCHEMA, {});
    expect(message).not.toBeNull();
    expect(message).toContain("must have required property 'text'");
  });

  it('rejects type mismatches', () => {
    const validator = new SchemaValidator();
    expect(validator.validate('echo', ECHO_SCHEMA, { text: 42 })).not.toBeNull();
  });

  it('skips validation when no schema is published', () => {
    const validator = new SchemaValidator();
    expect(validator.validate('no-schema-tool', undefined, { anything: true })).toBeNull();
  });

  it('caches compiled validators per tool', () => {
    const validator = new SchemaValidator();
    validator.validate('echo', ECHO_SCHEMA, { text: 'a' });
    validator.validate('echo', ECHO_SCHEMA, { text: 'b' });
    expect(validator.validate('echo', ECHO_SCHEMA, {})).not.toBeNull();
  });

  it('recompiles against a changed schema for the same tool after clear()', () => {
    const validator = new SchemaValidator();
    // First manifest: text is required.
    expect(validator.validate('echo', ECHO_SCHEMA, {})).not.toBeNull();
    // Second manifest: text is no longer required; count becomes required.
    const changed: Record<string, unknown> = {
      type: 'object',
      properties: { text: { type: 'string' }, count: { type: 'integer' } },
      required: ['count'],
      additionalProperties: false,
    };
    validator.clear();
    expect(validator.validate('echo', changed, { text: 'hi' })).not.toBeNull(); // count missing
    expect(validator.validate('echo', changed, { count: 1 })).toBeNull();
  });

  it('handles NJsonSchema-flavoured schemas (draft style with $schema)', () => {
    const njs = {
      $schema: 'http://json-schema.org/draft-07/schema#',
      type: 'object',
      properties: { name: { type: 'string' } },
      required: ['name'],
    };
    const validator = new SchemaValidator();
    expect(validator.validate('cogo', njs, { name: 'A-1' })).toBeNull();
    expect(validator.validate('cogo', njs, {})).not.toBeNull();
  });
});

describe('splitControlArgs', () => {
  it('extracts confirm and progress token, leaving the tool arguments clean', () => {
    const { toolArgs, confirm, progressToken } = splitControlArgs({
      id: 7,
      newName: 'Road A',
      confirm: true,
      _meta: { progressToken: 'tok-1' },
    });
    expect(toolArgs).toEqual({ id: 7, newName: 'Road A' });
    expect(confirm).toBe(true);
    expect(progressToken).toBe('tok-1');
  });

  it('returns undefined control fields when absent', () => {
    const { toolArgs, confirm, progressToken } = splitControlArgs({ id: 1 });
    expect(toolArgs).toEqual({ id: 1 });
    expect(confirm).toBeUndefined();
    expect(progressToken).toBeUndefined();
  });
});
