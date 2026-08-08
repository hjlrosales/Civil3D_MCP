# Autodesk.MCP.Server

The production MCP server that exposes the `Civil3D.Bridge` tool catalog to MCP clients.

It is a Node.js 20+ TypeScript application built on the official
`@modelcontextprotocol/sdk`. It discovers bridge instances from the endpoint registry,
speaks the Autodesk MCP named-pipe JSON-RPC protocol, and registers every discovered tool
dynamically as an MCP tool. **No tool is hardcoded.**

```
MCP Client  <--stdio (JSON-RPC)-->  Autodesk.MCP.Server (Node)
                                     | McpAdapter                   |
                                     | BridgeManager                |
                                     | BridgeClient                 |
                                     | BridgeConnection             |
                                     Windows named pipe (NDJSON)
                                     Civil3D.Bridge (in AutoCAD/Civil 3D)
```

- **Location:** `src/server/Autodesk.Mcp.Server`
- **Language / runtime:** TypeScript (ESM, Node.js >= 20)
- **Key dependencies:** `@modelcontextprotocol/sdk`, `ajv` (argument-schema validation), `pino` (logging), `vitest` (tests)

---

## Architecture

The server is layered so that each concern is isolated and individually testable.

| Layer | Module | Responsibility |
| --- | --- | --- |
| MCP | `src/mcp/mcpAdapter.ts` | Registers MCP handlers (`tools/list`, `tools/call`, `ping`) on the SDK `Server`; validates arguments; maps bridge envelopes to MCP results; forwards progress and cancellation. |
| MCP | `src/mcp/schema.ts` | Compiles and caches ajv validators from the bridge's JSON Schema per tool. |
| MCP | `src/mcp/errors.ts` | Maps bridge failures to structured `isError` tool results; keeps the bridge error code visible to the AI client. |
| Manager | `src/manager.ts` | Selects the bridge to talk to (product/bridge preferences, recency), owns the reconnect loop with backoff, and re-emits `manifest` / `progress` / `status` events. |
| Bridge | `src/bridge/bridgeClient.ts` | One authenticated session to a bridge: handshake, session id, `tools/list` manifest loading with diff caching, `tools/execute`, `$/cancel`, progress events. |
| Discovery | `src/discovery/endpointStore.ts` | Scans `%LOCALAPPDATA%\AutodeskMcp\endpoints`, parses descriptors, checks pid liveness, cleans stale files, and selects the best endpoint. |
| Discovery | `src/discovery/monitor.ts` | Polls the registry and emits `update` when the endpoint set changes (appear / disappear / replace). |
| Transport | `src/transport/bridgeConnection.ts` | One named-pipe session: NDJSON framing, request/response correlation by correlation id, per-request timeouts, heartbeat, cancellation, and rejection of in-flight requests on disconnect. |
| Transport | `src/transport/ndjson.ts` / `pipe.ts` | Newline-delimited JSON framing with size guards; Windows named-pipe path resolution and connection. |
| Protocol | `src/protocol/*` | TypeScript mirrors of the shared wire contracts (`RequestEnvelope`, `ResponseEnvelope`, `Manifest`, `ToolManifest`, `EndpointDescriptor`, ...), method/notification constants, and SemVer helpers. |
| Config / Logging | `src/config.ts`, `src/logger.ts`, `src/index.ts` | Configuration file + environment variables, pino logging to **stderr** (stdout stays clean for MCP), and the stdio entrypoint. |

### Execution path for one `tools/call`

1. The MCP client calls `tools/call { name, arguments }`.
2. `McpAdapter` looks up the tool in the current manifest, strips MCP-reserved control fields (`_meta`, `confirm`) from the arguments, and validates the remainder against the bridge's input schema (ajv). Invalid arguments become a JSON-RPC `InvalidParams` error (`-32602`).
3. A fresh correlation id is created. If the client supplied `_meta.progressToken`, the token is remembered for that correlation so progress can be forwarded back to the right client request.
4. `BridgeClient.execute(toolName, args)` sends `tools/execute` over the named pipe. The bridge envelope is returned whether it is a success or a business failure.
5. Success payloads are serialized as text content; failures become an `isError: true` result whose JSON keeps the bridge error code (`E_...`) so clients can react programmatically. A confirmation-required failure carries `confirmation.retryWith: { confirm: true }`.
6. While the tool runs, bridge `$/progress` notifications are forwarded as MCP `notifications/progress` (with the progress token). Client `notifications/cancelled` aborts the SDK request signal, which forwards `$/cancel` to the bridge.

---

## Bridge discovery

Bridges publish a JSON descriptor file into the endpoint registry:

```
%LOCALAPPDATA%\AutodeskMcp\endpoints\<product>-<pid>.json
```

Example descriptor (`EndpointDescriptor`):

```json
{
  "bridgeName": "Civil3D.Bridge",
  "product": "Civil3D",
  "productVersion": "2026",
  "bridgeVersion": "1.0.0",
  "sdkVersion": "1.0.0",
  "protocolVersion": "1.0.0",
  "pipeName": "autodesk-mcp-civil3d-12345",
  "pid": 12345,
  "startedUtc": "2026-08-07T10:00:00.000Z"
}
```

The server never hardcodes pipe names. The monitor polls the registry every
`endpointsPollIntervalMs` and:

- supports **multiple bridges** (multiple Autodesk products, multiple instances of one product);
- removes **stale descriptors** (files whose pid is no longer a live process) on every poll;
- picks the **most recently started** endpoint after applying preferences
  (`preferredProduct`, then `preferredBridge`).

When the selected endpoint changes - including after the connected bridge restarts and
republishes - the manager disconnects the old session and connects to the new endpoint.

---

## Configuration

Precedence: **defaults -> configuration file -> environment variables**.

### Configuration file

Path is `--config <path>` / `-c <path>` on the command line, or the
`AUTODESK_MCP_CONFIG` environment variable. A malformed or missing file is ignored
(defaults apply).

```json
{
  "logLevel": "info",
  "endpointsDir": "C:\Users\you\AppData\Local\AutodeskMcp\endpoints",
  "preferredProduct": "Civil3D",
  "preferredBridge": "Civil3D.Bridge",
  "reconnectDelayMs": 1000,
  "maxReconnectAttempts": 10,
  "requestTimeoutMs": 30000,
  "heartbeatIntervalMs": 15000,
  "endpointsPollIntervalMs": 3000,
  "clientName": "Autodesk.MCP.Server",
  "clientVersion": "1.0.0"
}
```

### Environment variables

| Variable | Overrides |
| --- | --- |
| `AUTODESK_MCP_LOG_LEVEL` | `logLevel` |
| `AUTODESK_MCP_ENDPOINTS_DIR` | `endpointsDir` |
| `AUTODESK_MCP_PREFERRED_PRODUCT` | `preferredProduct` |
| `AUTODESK_MCP_PREFERRED_BRIDGE` | `preferredBridge` |
| `AUTODESK_MCP_RECONNECT_DELAY_MS` | `reconnectDelayMs` |
| `AUTODESK_MCP_MAX_RECONNECT_ATTEMPTS` | `maxReconnectAttempts` |
| `AUTODESK_MCP_REQUEST_TIMEOUT_MS` | `requestTimeoutMs` |
| `AUTODESK_MCP_HEARTBEAT_INTERVAL_MS` | `heartbeatIntervalMs` |
| `AUTODESK_MCP_ENDPOINTS_POLL_INTERVAL_MS` | `endpointsPollIntervalMs` |

### Option semantics

| Option | Default | Meaning |
| --- | --- | --- |
| `reconnectDelayMs` | 1000 | Base delay before reconnecting; doubles after each failed attempt. |
| `maxReconnectAttempts` | 10 | Reconnect attempts before giving up (`0` = retry forever). Discovery still reconnects whenever a usable endpoint appears. |
| `requestTimeoutMs` | 30000 | Per-request timeout for bridge calls. |
| `heartbeatIntervalMs` | 15000 | `health/ping` interval (`0` disables the heartbeat). |
| `endpointsPollIntervalMs` | 3000 | Registry polling interval. |

---

## Logging

Logging uses **pino** and writes exclusively to **stderr** - stdout carries MCP JSON-RPC
traffic, so it must stay clean.

- `--logLevel` / `AUTODESK_MCP_LOG_LEVEL` selects `trace|debug|info|warn|error|fatal`.
- Every log line is structured JSON with a `component: "autodesk-mcp-server"` base and
  ISO timestamps.
- Instrumented events include: startup, endpoint selection, connect/reconnect attempts
  (with backoff), bridge disconnect, manifest loads, tool execution (correlation id and
  duration), cancellation forwarding, progress forwarding, and shutdown.

---

## Reconnect behavior

1. The monitor emits a registry change; the manager selects the best endpoint.
2. If the endpoint changed, the old session is closed (in-flight requests reject with
   `E_BRIDGE_UNAVAILABLE`) and a new connection is attempted.
3. Failed connects retry with exponential backoff (`reconnectDelayMs` doubling, capped by
   `maxReconnectAttempts`). A heartbeat failure or a dropped pipe also triggers the loop.
4. On connect, the manager performs the handshake and reloads the manifest; `tools/list`
   reflects the new catalog immediately.

Because discovery and the reconnect loop are independent, the server also recovers when a
bridge exits and a **new** instance registers later, without exhausting the reconnect
budget.

---

## Client compatibility

- **MCP version:** the SDK server speaks the current MCP protocol (`initialize`,
  `tools/list`, `tools/call`, `ping`, `notifications/progress`, `notifications/cancelled`).
- **Dynamic discovery:** `tools/list` returns exactly the tools published by the connected
  bridge manifest, including their JSON Schema input schemas and annotations
  (`title`, `readOnlyHint`, `destructiveHint`).
- **Confirmation:** bridge `E_CONFIRMATION_REQUIRED` failures are returned as structured
  results with a `confirmation.retryWith: { confirm: true }` field. Clients that support
  MCP elicitation can surface the confirmation; clients that do not can simply retry with
  the `confirm: true` argument. The `confirm` and `_meta` fields are stripped from tool
  arguments before validation and are never sent to the bridge as tool input.
- **Progress:** bridge `$/progress` notifications become MCP `notifications/progress`
  events addressed to the originating client's progress token.
- **Cancellation:** MCP `notifications/cancelled` becomes a bridge `$/cancel`
  notification for the matching correlation id.

---

## Building and testing

```bash
cd src/server/Autodesk.Mcp.Server
npm install          # or: node /tmp/npm/package/bin/npm-cli.js install
npm run typecheck    # tsc --noEmit
npm run build        # tsc -p tsconfig.build.json -> dist/
npm test             # vitest run
```

The test suite (60 tests across 8 files) includes:

- **Unit tests** - SemVer parsing/formatting, NDJSON framing, endpoint-store parsing /
  selection / stale cleanup, ajv schema validation, manifest diffing.
- **Integration tests** - a protocol-faithful fake bridge listening on a real Windows
  named pipe verifies the full path: discovery -> handshake -> manifest loading -> routing ->
  execution -> protocol response, plus confirmation retry, progress forwarding, client
  cancellation, reconnect after bridge restart, and multi-instance selection.

## Running

```bash
node dist/index.js [--config <path>]
```

Requires the `Civil3D.Bridge` loaded inside a running Civil 3D session (which writes its
endpoint descriptor). MCP clients connect over stdio as usual.
