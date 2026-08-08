# JSON-RPC wire examples

Real bridge-protocol messages, newline-delimited JSON (NDJSON) over the Windows
named pipe. The envelope is defined in `Autodesk.Mcp.Shared` (C#) and mirrored in
`src/server/Autodesk.Mcp.Server/src/protocol`.

Format (requests):

```json
{
  "method": "tools/execute",
  "id": 1,
  "params": { "tool": "drawing_info", "arguments": {} },
  "correlationId": "6f8c4e2a-9c1d-4f5a-8b3e-2a1f0c7d9e01",
  "sessionId": "sess-1",
  "timeoutMilliseconds": 30000,
  "clientRequestedAtUtc": "2026-08-08T09:15:02.123Z"
}
```

Responses use the frozen envelope:

```json
{
  "success": true,
  "message": "",
  "executionTime": 8,
  "errorCode": "",
  "correlationId": "6f8c4e2a-9c1d-4f5a-8b3e-2a1f0c7d9e01",
  "sessionId": "sess-1",
  "data": {}
}
```

| File | Contents |
| --- | --- |
| `handshake.jsonl` | startup version negotiation + session |
| `tools-list.jsonl` | full tool catalog exchange |
| `tools-execute.jsonl` | read-only tool call |
| `confirmation.jsonl` | editing tool rejected, then retried with `confirm: true` |
| `progress-cancel.jsonl` | long-running tool with progress + cancellation |
| `shutdown.jsonl` | graceful shutdown |

The `$`-prefixed methods are notifications (no `id`, no reply): `$/progress`,
`$/cancel`.
