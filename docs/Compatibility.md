# Compatibility

What works with what, and the guarantees we make.

---

## Autodesk products

| Product | Versions | Notes |
| --- | --- | --- |
| Autodesk Civil 3D | 2025, 2026 | compiled against the 2025 SDK; API-compatible |
| AutoCAD (base) | 2025, 2026 | the bridge runs, but Civil 3D tools need Civil 3D objects |

## Runtime

| Component | Runtime | Notes |
| --- | --- | --- |
| Bridge | .NET 8 (Desktop) | ships with Civil 3D 2025/2026 |
| Server | Node.js >= 20 | Windows required (named pipes) |
| OS | Windows 10/11 x64 | pipes are per-user, local only |

## MCP clients

Any client that speaks MCP over **stdio** works. Tested configurations:

- Claude Desktop
- VS Code (MCP extension)
- Cursor
- Cline

See `examples/clients/` for ready-made configs. The server uses standard MCP
features only: `tools/list`, `tools/call`, `notifications/progress`,
`notifications/cancelled`, and structured error results (`isError`). Clients that
support MCP elicitation can drive confirmation; clients that do not can retry with
the `confirm` argument on editing tools.

## Protocol compatibility

- The **bridge protocol** (server <-> bridge over the named pipe) is versioned by
  `protocolVersion` in the handshake. A **major** mismatch is refused with a clear
  error. Minor differences are tolerated.
- The **MCP protocol** is provided by `@modelcontextprotocol/sdk`; the server
  negotiates the MCP version at `initialize`.
- The wire envelope (success/message/executionTime/errorCode/data) is frozen for
  protocol 1.x. Adding fields is allowed; removing or renaming is a breaking change.

## Version skew matrix

> The 1.0.0-rc.2 rows below reflect the Phase 9 hardening baseline. The version
> bump from 1.0.0-rc.1 to 1.0.0-rc.2 is applied at release time via
> `eng/scripts/sync-version.mjs`; until then the built artifacts carry
> 1.0.0-rc.1.

| Server | Bridge | Result |
| --- | --- | --- |
| 1.0.0-rc.2 | 1.0.0-rc.2 | fully supported |
| 1.0.0-rc.2 | 1.0.x (any) | supported while protocol major = 1 |
| 1.0.0-rc.2 | older bridge (same major) | supported; unknown fields/enum values tolerated (forward compatible) |
| protocol major differs | - | refused at handshake with guidance |

## Hardening-verified compatibility (Phase 9)

- SemVer negotiation is exercised by automated tests: the handshake advertises
  `protocolVersion`, unknown manifest fields and unknown enum values are
  tolerated, and a protocol **major** mismatch is refused with a structured
  error that never leaks a raw exception.
- Re-loading an identical manifest is idempotent (no duplicate tool
  registrations).
- Bridge and server versions are synchronized from `eng/version.json`; the
  version-drift gate fails the build if any artifact falls out of sync.

See `docs/PRODUCTION-HARDENING.md` for the full hardening report.

## Data / units

- Units are the drawing's native units; tools report what Civil 3D reports
  (meters/feet per drawing setup).
- Coordinates are in the active UCS/world coordinate system as exposed by the API.

## Not supported (yet)

- macOS / Linux hosting (named pipes are Windows-only).
- Remote/network bridge access (by design - local security boundary).
- Batch/parallel tool execution (`supportsBatchRequests` / `supportsParallelExecution`
  are advertised as false).
- Products other than Civil 3D (architecture is ready; bridges are future work).
