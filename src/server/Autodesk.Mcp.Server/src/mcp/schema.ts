import { createRequire } from 'node:module';
import type { ValidateFunction } from 'ajv';

/**
 * The ajv package ships CommonJS while its types are ESM-flavoured; under NodeNext the default
 * import resolves to the namespace, so the constructor is obtained through createRequire (which
 * returns the real module.exports at runtime).
 */
const require = createRequire(import.meta.url);
const AjvCtor = require('ajv') as new (options: Record<string, unknown>) => InstanceType<typeof import('ajv').default>;

/**
 * Validates tool arguments against the JSON Schema published by the bridge manifest. Compiled
 * validators are cached per tool; MCP-reserved control fields (_meta, confirm) are stripped
 * before validation because they are not part of the bridge input DTOs.
 */
export class SchemaValidator {
  private readonly ajv = new AjvCtor({ allErrors: true, strict: false, validateFormats: false });
  private readonly cache = new Map<string, ValidateFunction>();

  /** Drops all compiled validators; call when a new manifest replaces the old one. */
  clear(): void {
    this.cache.clear();
  }

  /**
   * Validates arguments against a tool's input schema. Returns null when valid; otherwise a
   * human-readable validation message listing up to three failing paths.
   */
  validate(toolName: string, schema: Record<string, unknown> | undefined, args: unknown): string | null {
    if (schema === undefined) {
      return null;
    }

    let validate = this.cache.get(toolName);
    if (validate === undefined) {
      validate = this.ajv.compile(schema as Record<string, unknown>);
      this.cache.set(toolName, validate);
    }

    const valid = validate(args);
    if (valid) {
      return null;
    }

    const errors = validate.errors ?? [];
    const messages = errors
      .slice(0, 3)
      .map((error) => `${error.instancePath || '/'} ${error.message ?? 'is invalid'}`);
    return messages.join('; ');
  }
}

/** Splits MCP control fields out of the tool arguments before they reach the bridge. */
export function splitControlArgs(args: Record<string, unknown>): {
  toolArgs: Record<string, unknown>;
  confirm?: boolean;
  progressToken?: string | number;
} {
  const meta = (args as { _meta?: Record<string, unknown> })._meta;
  const confirm = typeof args.confirm === 'boolean' ? args.confirm : undefined;

  const toolArgs: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(args)) {
    if (key === '_meta' || key === 'confirm') {
      continue;
    }
    toolArgs[key] = value;
  }

  const progressToken = meta !== undefined && typeof meta === 'object' && !Array.isArray(meta)
    ? meta.progressToken
    : undefined;
  return {
    toolArgs,
    confirm,
    progressToken: typeof progressToken === 'string' || typeof progressToken === 'number'
      ? progressToken
      : undefined,
  };
}
