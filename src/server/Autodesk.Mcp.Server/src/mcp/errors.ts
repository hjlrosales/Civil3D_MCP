import { ErrorCode } from '../protocol/types.js';

/** The MCP JSON-RPC error codes this server emits. */
export const McpErrorCodes = {
  InvalidParams: -32602,
  ServerError: -32000,
} as const;

/** True when the bridge asked for explicit user confirmation before proceeding. */
export function isConfirmationRequired(code: string | undefined): boolean {
  return code === ErrorCode.E_CONFIRMATION_REQUIRED;
}

/** Builds the structured, machine-readable tool result for a bridge failure. */
export function bridgeFailureContent(code: string | undefined, message: string | undefined, correlationId?: string): {
  type: 'text';
  text: string;
} {
  const payload: Record<string, unknown> = {
    code: code ?? ErrorCode.E_UNKNOWN,
    message: message ?? 'The bridge reported a failure.',
  };
  if (correlationId !== undefined) {
    payload.correlationId = correlationId;
  }
  if (isConfirmationRequired(code)) {
    payload.confirmation = {
      required: true,
      retryWith: { confirm: true },
      hint: 'Retry the tool call with a confirm: true argument to acknowledge the confirmation.',
    };
  }
  return { type: 'text', text: JSON.stringify(payload, null, 2) };
}

/** Renders a successful bridge payload as MCP text content. */
export function successContent(data: unknown): { type: 'text'; text: string } {
  return { type: 'text', text: data === undefined ? '' : JSON.stringify(data, null, 2) };
}
