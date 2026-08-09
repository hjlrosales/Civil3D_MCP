# Production Hardening (Phase 9)

This document records the Phase 9 production-hardening work performed between
Release Candidate 1 and Release Candidate 2: failure & recovery testing,
concurrency testing, stress testing, resource-leak inspection, security review,
version compatibility, packaging validation, MCP client compatibility, and
operational diagnostics.

The objective of Phase 9 was to **attempt to break RC1**. No new engineering
features or MCP tools were added. Every defect discovered is covered by an
automated regression test and the full quality gate must pass before RC2.

---

## 1. Test strategy

Phase 9 added eight automated hardening suites on the server (TypeScript / vitest),
plus a fresh-install packaging validation script, and wired them into the local
quality gate and CI.

| Suite | File | Coverage |
| --- | --- | --- |
| Failure & recovery | `src/server/Autodesk.Mcp.Server/test/recovery.test.ts` | pipe drop, malformed NDJSON / JSON-RPC, unknown & duplicate request ids, timeouts, shutdown during execution, stale/PID-reuse descriptors, multi-instance churn |
| Concurrency | `test/concurrency.test.ts` | multiple MCP clients against one bridge, concurrent calls, correlation uniqueness, session isolation |
| Cancellation isolation | `test/concurrency-cancel.test.ts` | cancelling one request never cancels a concurrent sibling |
| Security | `test/security.test.ts` | payload limits, oversized messages, exception containment across the protocol boundary |
| Version compatibility | `test/compatibility.test.ts` | semver negotiation, unknown fields/enum values, protocol major mismatch, duplicate manifest load |
| Resource leaks | `test/resourceLeaks.test.ts` | repeated start/stop, connect/disconnect and execution cycles with no lingering state |
| Stress | `test/stress.test.ts` | large manifests (up to 1,000 tools), many concurrent calls through the real stack, response-size accounting |
| Diagnostics | `test/diagnostics.test.ts` | correlation id, tool name, bridge selection and reconnect events are present in operator logs |

Plus:

- `eng/scripts/validate-fresh-install.mjs` - clean-machine packaging validation
  (fresh `npm pack` + `npm install` into a temp dir, CLI `--version`/`--help`,
  start/stop with an empty endpoint registry, bundle zip integrity).
- The existing e2e suite (7 suites, 13 tests) already covers startup, handshake,
  discovery, execution, failures, confirmation, progress, cancellation, reconnect,
  shutdown and multi-instance selection against a protocol-faithful fake bridge.

### Running the hardening suites

```bash
npm --prefix src/server/Autodesk.Mcp.Server test            # all server tests incl. hardening
node eng/scripts/validate-fresh-install.mjs                 # fresh-install validation
node eng/scripts/quality-gate.mjs                           # full gate incl. hardening suites
```

---

## 2. Failure & recovery scenarios

Each scenario below was exercised by an automated test. The right-hand column
states the observed recovery behavior.

| Scenario | Observed behavior |
| --- | --- |
| MCP server restart | Clean start; re-discovers endpoints from the registry and reconnects. No stale session state. |
| Civil 3D Bridge restart | Endpoint descriptor disappears; the manager stops routing to it, then reconnects when the bridge returns. |
| Named pipe disconnect | The connection emits a protocol error; in-flight requests settle with an error (never hang); reconnect is attempted. |
| Pipe connection failure | `connect()` rejects with a structured error; the manager keeps retrying per its backoff policy. |
| Partial request transmission | The framing layer buffers incomplete NDJSON lines and only dispatches a complete message. |
| Malformed NDJSON | Rejected as a protocol error; the connection is not corrupted and subsequent valid messages still flow. |
| Invalid JSON-RPC | Rejected with a stable error code; no exception crosses the protocol boundary. |
| Unknown request id (no correlation match) | The response is dropped with a warning; the request eventually times out and settles. No orphaned promise. |
| Duplicate request id | Both requests settle (the duplicate is not silently swallowed); no response crosses request boundaries. |
| Request timeout | The pending request rejects with a timeout error; the per-request timer is removed. |
| Tool timeout | The bridge envelope reports a timeout error code mapped to a stable MCP error; the dispatcher cancels the work item. |
| Client cancellation | Forwarded to the bridge as `$/cancel` with the exact correlation id; only that request is affected. |
| Bridge cancellation | Propagated back to the MCP client as a structured error result. |
| Shutdown during active execution | In-flight work is cancelled via the dispatcher shutdown token; the process exits without deadlock. |
| Shutdown with queued executions | Queued work items are drained/cancelled; no work is executed after shutdown begins. |
| Bridge disappearing during execution | The in-flight envelope settles with a connection error; the manager transitions to reconnecting. |
| Endpoint descriptor disappearing | The endpoint is removed from the registry view; the manager refuses to route to it and re-polls. |
| Stale endpoint descriptor | Stale descriptors (heartbeat expiry / old start time) are superseded; the freshest bridge wins. |
| PID reuse | The endpoint is matched by pipe identity plus start time; a reused PID does not confuse routing. |
| Multiple bridges appearing/disappearing | The manager converges to the preferred/freshest endpoint and re-evaluates on every poll. |

### Verified recovery guarantees

- **No deadlocks**: every await path has a timeout or a connection-level failure
  signal; the recovery suite asserts in-flight promises always settle.
- **No orphaned requests**: unknown and duplicate correlation ids settle; the
  request registry is drained on connection close and shutdown.
- **No leaked resources**: repeated connect/disconnect cycles leave no pending
  requests or lingering pipe handles (see section 5).
- **No corrupted sessions**: a protocol error tears down only the affected
  connection; the next connect starts a fresh session.
- **No duplicate tool registrations**: re-loading an identical manifest produces
  the same tool set (regression-tested); a changed manifest replaces validators.
- **No permanently stuck connections**: the manager reconnects with bounded
  attempts and backoff, and the client surfaces the state through logs and errors.
- **No terminal give-up**: exhausting the reconnect budget parks the endpoint for
  `retryCooldownMs` instead of abandoning it. Because discovery re-evaluates on
  every poll (not only on registry changes), a bridge that becomes reachable later
  is always picked up without restarting the server.
- **No stale catalog**: losing the bridge clears the advertised tools and notifies
  the MCP client, so `tools/list` never offers tools that cannot execute.
- **No silently empty client**: the server declares `tools.listChanged` and pushes
  `notifications/tools/list_changed` on every catalog transition, so a client that
  listed tools before any bridge existed still converges on the real catalog. The
  lifecycle suite covers both start-up orderings and every appear/disappear cycle.

---

## 3. Concurrency model

### What is serialized (by design)

- **Bridge tool execution is FIFO-serialized** inside the dispatcher
  (`ToolDispatcher` in `Civil3D.Bridge`). This is intentional: Autodesk objects
  are not thread-safe and editing must run on the application/transaction thread.
  Read-only tools also serialize behind this queue - a documented trade-off that
  keeps thread safety simple and predictable.
- **Endpoint registration** uses a semaphore so descriptors never interleave.

### What runs concurrently

- Multiple MCP clients (each with its own SDK `Server` and transport) can attach
  to one server instance and execute tools concurrently; their requests queue at
  the bridge but are correlated independently.
- Multiple bridge instances are discovered and one is selected; sessions stay
  isolated per connection.
- Progress notifications and cancellations are routed by correlation id, so a
  cancellation affects **only** the intended request.

### Verified guarantees (concurrency suite)

- **Correlation ids remain unique**: every `tools/call` allocates a fresh UUID;
  the suite asserts no two in-flight requests share one.
- **Responses never cross request boundaries**: each pending request is keyed by
  correlation id and settles with its own envelope.
- **Sessions remain isolated**: each connection gets its own session id; closing
  one session does not disturb another.
- **Cancellation is request-scoped**: cancelling a slow call leaves a concurrent
  sibling running to completion (regression-tested with controlled delays).
- **Editing serialization**: the dispatcher runs one work item at a time; the
  suite verifies concurrent edit calls complete in submission order.

---

## 4. Performance & stress findings

### Stress fixtures

- **Small manifest**: sample 3-tool manifest.
- **Large manifest**: generated 500-tool and 1,000-tool manifests with realistic
  input schemas, loaded through the real server stack.
- **Concurrent load**: up to 50 simultaneous `tools/call` requests through real
  MCP clients against the manager + adapter + connection stack.

### Measured behaviour

| Observation | Result |
| --- | --- |
| 1,000-tool manifest load through the real stack | completes within test budget; `tools/list` reflects all tools |
| 50 concurrent calls | all settle; no timeouts at the configured request budget |
| Manifest size scaling | linear in tool count; the server streams NDJSON and does not buffer the full wire payload beyond the framing limit |
| Response size | bounded by envelope; large results (e.g. quantity/surface data) stream as a single NDJSON line within the configured framing limit |

### Pathological scaling

No operation with pathological (super-linear) scaling was found in the tested
range. The two deliberate bottlenecks are documented rather than hidden:

1. **Bridge dispatcher FIFO queue** - throughput is bounded by the slowest
   single execution; this is the documented thread-safety trade-off.
2. **Framing buffer** - a single NDJSON line is the largest atomic unit; very
   large tool results are limited by `MaxMessageLength` (see security section).

Per the Phase 9 mandate, no premature optimization was performed: only
correctness defects were fixed. For benchmark numbers across startup, handshake,
manifest load, pipe throughput and memory, see `docs/PERFORMANCE-BENCHMARKS.md`.

---

## 5. Resource management & leak testing

### Inspected areas

- Undisposed Autodesk objects, transactions and `DocumentLock`s (bridge side;
  transactions are created inside `using` blocks and disposed by the command
  pipeline).
- Abandoned named pipes: the server closes its connection on protocol errors and
  on shutdown; repeated connect/disconnect cycles leave no pending requests.
- Orphaned worker tasks: the dispatcher drains its channel on shutdown and
  cancels work items with its shutdown token.
- `CancellationToken` registrations: cancellation registrations are removed in
  the adapter `finally` block after every `tools/call`.
- Timers: the manager clears polling/heartbeat timers on `stop()`.
- File handles: the endpoint registry directory is only read (no lingering
  writer handles on the server); the bridge owns descriptor writes.
- Lingering endpoint descriptors: the manager re-reads the registry each poll and
  drops stale entries from its view; descriptors are cleaned by the OS/user.
- Process handles: repeated server shutdown produces a clean exit code.

### Regression tests

`test/resourceLeaks.test.ts` runs 10 connect/disconnect cycles, repeated manager
start/stop cycles and repeated execution cycles, then asserts there are no
pending requests and no leaked state after cleanup. The e2e suite asserts the
server process exits cleanly on SIGTERM.

---

## 6. Security review

### Current trust model (documented)

- **Local-only by construction**: the server talks to bridges exclusively over
  per-user Windows named pipes. The pipe is created with a `CurrentUserOnly`
  security descriptor, so no other Windows user can connect.
- **No authentication added**: per the Phase 9 mandate, authentication was not
  introduced. The trust boundary is the Windows user account: any process running
  as the same user can read/write the endpoint registry and connect to the pipe.
  This is the same trust model as other local-user tools and is documented so
  operators can decide whether it matches their threat model.
- **MCP stdio clients**: any local process that launches the server owns its
  stdin/stdout and therefore the full tool surface (equivalent to running the
  server yourself).

### Controls verified by test

| Area | Control |
| --- | --- |
| Named pipe ACLs | `CurrentUserOnly` descriptor on the pipe (bridge side). |
| Endpoint descriptor permissions | Descriptors live under the per-user `%LOCALAPPDATA%\AutodeskMcp\endpoints`; the server only reads them. |
| Configuration files | Loaded from an explicit path (CLI/env); no secrets are stored in config. |
| Output path handling | Tool outputs are returned as structured data over the pipe; the server never writes tool results to disk. |
| Path traversal | The server never derives a filesystem path from tool arguments; no file write surface exists on the server. |
| Logging of sensitive paths | Logs include pipe names and endpoint dirs (needed for diagnostics) but not drawing contents or arguments. |
| Exception leakage | Raw Autodesk exceptions and stack traces never cross the protocol boundary; the bridge maps them to stable error codes (see below). |
| Protocol injection | Messages are validated NDJSON + JSON-RPC; malformed input is rejected with a protocol error and cannot inject frames (framing is length-delimited by newline). |
| Malformed schema handling | Tool argument validation is defensive: non-object arguments degrade to `{}`, unknown control args are ignored, and schemas are re-validated per manifest. |
| JSON payload size | Framing enforces `MaxMessageLength`; oversized messages are rejected instead of buffered indefinitely. |
| Manifest size | Manifest loading streams and validates; the stress suite exercises 1,000-tool manifests. |
| Tool argument size | Bounded by the framing limit; oversized argument payloads are rejected at the connection layer. |
| DoS resilience | Repeated malformed messages do not corrupt the connection; reconnect attempts are bounded and back off. |

### Error-code containment

Bridge failures and connection failures are converted to stable codes
(`E_INVALID_REQUEST`, `E_TOOL_TIMEOUT`, `E_NO_ACTIVE_DOCUMENT`, etc.) in the
envelope; the MCP adapter maps these to `isError` results with the code and a
safe message. Stack traces from the bridge stay in the bridge log only.

---

## 7. Version compatibility

### Protocol versioning

- The bridge handshake carries `protocolVersion` (SemVer). The server refuses a
  **major** mismatch with a clear structured error; minor differences are
  tolerated.
- The MCP protocol version is negotiated by `@modelcontextprotocol/sdk` at
  `initialize`; the server advertises only standard capabilities.
- The wire envelope for protocol 1.x is frozen: adding fields is allowed,
  removing/renaming is breaking.

### Verified scenarios (compatibility suite)

| Scenario | Result |
| --- | --- |
| Handshake advertises the current protocol version | PASS |
| Unknown manifest fields and unknown enum values (forward compatibility) | PASS - tolerated |
| Protocol major mismatch | refused with a structured `E_INVALID_REQUEST`-style error |
| Bridge-side protocol rejection surfaces to the client | PASS - structured error, no raw exception |
| Re-loading an identical manifest | no duplicate tools; same tool set |

### Compatibility matrix

| Server | Bridge | Result |
| --- | --- | --- |
| 1.0.0-rc.2 | 1.0.0-rc.2 | fully supported |
| 1.0.0-rc.2 | 1.0.x (any) | supported while protocol major = 1 |
| protocol major differs | - | refused at handshake with guidance |

Civil 3D 2025 and 2026 are both in the supported matrix (compiled against the
2025 SDK; API-compatible). See `docs/Compatibility.md` for the full matrix.

---

## 8. Packaging validation (clean-machine style)

`eng/scripts/validate-fresh-install.mjs` performs the following checks without
relying on the developer's environment:

1. **Fresh environment**: a new temp directory is created for each run.
2. **Install server**: `npm pack` produces a tarball, which is `npm install`-ed
   into a second clean directory (no workspace links, no shared node_modules).
3. **CLI smoke**: the installed binary reports `--version` and `--help`.
4. **Server start**: the installed entry point starts and reaches the ready
   state with an empty endpoint registry, then terminates (the process is
   signalled after the ready check; graceful-shutdown exit codes are asserted
   by the e2e suite instead).
5. **Bundle integrity**: the bridge bundle zip opens cleanly (validated with
   `System.IO.Compression`) and contains `PackageContents.xml` with the expected
   `AppVersion`.
6. **Cleanup**: all temp directories are removed.

The check runs in CI (`ci.yml` node job) and in the local quality gate
(`--node`), so packaging regressions block release.

> Note: the bridge bundle **install** into `%APPDATA%\Autodesk\ApplicationPlugins`
> and the Civil 3D restart/uninstall cycle require a live Civil 3D host and are
> validated manually per `docs/RELEASE-VALIDATION.md`.

---

## 9. MCP client compatibility

All clients speak standard MCP over stdio; the server uses only standard
features (`tools/list`, `tools/call`, `notifications/progress`,
`notifications/cancelled`, structured `isError` results, `initialize`
negotiation). Ready-made configs are in `examples/clients/`.

| Client | Config | Verified capabilities | Known limitations |
| --- | --- | --- | --- |
| Claude Desktop | `claude-desktop.json` | startup, initialize, tools/list, tools/call, isError results | Progress/cancel forwarded as standard MCP notifications; confirmation via MCP elicitation when supported |
| VS Code (MCP extension) | `vscode-mcp.json` | startup, initialize, tools/list, tools/call | Older VS Code MCP builds may not surface `notifications/progress`; editing confirmation then requires the `confirm` argument |
| Cursor | `cursor-mcp.json` | startup, initialize, tools/list, tools/call | Progress display depends on client version; retry with `confirm` when no elicitation support |
| Cline | `cline-mcp.json` | startup, initialize, tools/list, tools/call | Cline shows structured errors; confirmation-driven edits may need explicit `confirm: true` |

Protocol-level behaviour (progress forwarding, cancellation, reconnect,
multi-instance selection, confirmation flow) is covered end-to-end by the e2e
suite against a protocol-faithful fake bridge; the same wire behaviour is what
the clients above consume. Live-in-Civil-3D verification is tracked in
`docs/RELEASE-VALIDATION.md`.

---

## 10. Operational diagnostics

Logs are written by pino to **stderr** (stdout stays clean for MCP protocol
traffic). The diagnostics suite asserts the fields an operator needs:

| Field | Where | Example |
| --- | --- | --- |
| correlation id | every tool execution log line | `correlation 45111899-...` |
| tool name | success and failure lines | `Tool echo succeeded (...)` |
| stable error code + message | failure lines | `Tool x failed with E_NO_ACTIVE_DOCUMENT: ...` |
| bridge instance | connection and selection lines | `Connected to Civil3D.Bridge (session sess-1, protocol 1.0.0)` |
| pipe name | connection/reconnect lines | `Connecting to bridge on pipe amcp-... (attempt 1)` |
| execution duration | debug line per tool | `Tool echo completed in 12 ms (correlation ...)` |
| bridge status transitions | manager status events | `Bridge status changed to reconnecting` |

Operator scenarios resolvable from logs: bridge unavailable, pipe connection
failure, protocol mismatch, tool timeout, tool cancellation, Autodesk
transaction failure, invalid arguments, stale endpoint, client disconnect, and
server crash (the fatal handler prints a startup error to stderr and exits
non-zero).

---

## 11. Known limitations

- **FIFO serialization** of bridge execution is deliberate (Autodesk thread
  safety); high-throughput read workloads do not parallelize.
- **Windows-only** hosting for the bridge (named pipes); macOS/Linux cannot run
  the bridge, and the server on non-Windows cannot connect to Windows pipes.
- **Remote/network bridge access** is not supported by design (local security
  boundary).
- **Large result payloads** are limited by the framing `MaxMessageLength`;
  extremely large tool results may need client-side summarisation.
- **Confirmation UX** depends on MCP client support for elicitation; clients
  without it must pass `confirm: true` explicitly for editing tools.
- **Live Civil 3D verification** (install/uninstall cycles, actual 2025/2026
  drawings, real CAD client sessions) requires a licensed Civil 3D host and is
  executed per `docs/RELEASE-VALIDATION.md` rather than in CI.
- **PID reuse**: routing keys include the pipe plus start time; a PID that is
  reused within the heartbeat window is disambiguated by the fresh descriptor.

---

## 12. RC2 criteria status

See `docs/RELEASE-1.0.0-RC2.md` for the final report and the RC2 recommendation.
