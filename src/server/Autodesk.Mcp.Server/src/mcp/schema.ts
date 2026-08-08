import { createRequire } from 'node:module';
import type { ValidateFunction } from 'ajv';

/**
 * The ajv package ships CommonJS while its types are ESM-flavoured; under NodeNext the default
 * import resolves to the namespace, so the constructor is obtained through createRequire (which
 * returns the real module.exports at runtime).
 */
const require = createRequire(import.meta.url);
// The bridge generates every tool input schema with NJsonSchema's default draft-04 dialect, but
// ajv v8 only ships meta-schemas for draft-06/07/2019-09/2020-12. Draft-04 documents are
// normalized to draft-07 (which ajv v8 compiles natively) by stripping the $schema URI and
// converting the boolean exclusiveMinimum/exclusiveMaximum form to the numeric draft-07 form.
// The keyword subset NJsonSchema emits for the bridge DTOs is identical in both dialects.
const AjvCtor = require('ajv') as new (options: Record<string, unknown>) => InstanceType<typeof import('ajv').default>;

const Draft04SchemaUri = 'http://json-schema.org/draft-04/schema#';

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
      validate = this.ajv.compile(normalizeDraft04(schema));
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

/**
 * Converts a draft-04 schema into the draft-07 form ajv v8 compiles natively. Draft-04 documents
 * are identified by their $schema URI; any other dialect (or a bare schema) passes through
 * unchanged. The conversion is shallow: the bridge DTO schemas are flat objects with string/
 * number/enum/array properties, so per-property subschemas are the deepest unit that carries the
 * boolean exclusive bounds.
 */
function normalizeDraft04(schema: Record<string, unknown>): Record<string, unknown> {
  if (schema.$schema !== Draft04SchemaUri) {
    return schema;
  }

  const normalized: Record<string, unknown> = { ...schema };
  delete normalized.$schema;

  const properties = normalized.properties;
  if (properties !== undefined && properties !== null && typeof properties === 'object' && !Array.isArray(properties)) {
    const converted: Record<string, unknown> = {};
    for (const [name, value] of Object.entries(properties)) {
      converted[name] = normalizeExclusiveBounds(value);
    }
    normalized.properties = converted;
  }

  return normalized;
}

/** Converts draft-04 boolean exclusiveMinimum/exclusiveMaximum to the draft-07 numeric form. */
function normalizeExclusiveBounds(value: unknown): unknown {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    return value;
  }

  const subschema = value as Record<string, unknown>;
  if (typeof subschema.exclusiveMinimum !== 'boolean' && typeof subschema.exclusiveMaximum !== 'boolean') {
    return value;
  }

  const normalized: Record<string, unknown> = { ...subschema };
  if (typeof normalized.exclusiveMinimum === 'boolean') {
    if (normalized.exclusiveMinimum === true && typeof normalized.minimum === 'number') {
      normalized.exclusiveMinimum = normalized.minimum;
    } else {
      delete normalized.exclusiveMinimum;
    }
  }
  if (typeof normalized.exclusiveMaximum === 'boolean') {
    if (normalized.exclusiveMaximum === true && typeof normalized.maximum === 'number') {
      normalized.exclusiveMaximum = normalized.maximum;
    } else {
      delete normalized.exclusiveMaximum;
    }
  }
  return normalized;
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
