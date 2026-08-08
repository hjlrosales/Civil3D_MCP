# Release Notes - Autodesk MCP Platform 1.0.0

- **Version**: 1.0.0 (GA / production release)
- **Date**: 2026-08-08
- **Git tag**: `v1.0.0`
- **Upgraded from**: 1.0.0-rc.2 (RC2, validated by the Phase 9 hardening report)

---

## Release overview

Autodesk MCP Platform v1.0.0 is the first official production release. It lets any
MCP-compatible AI assistant read and edit **Autodesk Civil 3D** drawings through
an MCP server (`autodesk-mcp-server`) and an in-process bridge plugin
(`Civil3D.Bridge`) loaded inside Civil 3D, communicating over a local Windows
named pipe.

The 1.0.0 release promotes the RC2 implementation unchanged apart from version
synchronization and release-artifact cleanup. No new features, tools or protocol
changes were introduced.

## Supported Autodesk versions

| Product | Versions | Notes |
| --- | --- | --- |
| Autodesk Civil 3D | 2025, 2026 | compiled against the 2025 SDK; API-compatible |
| AutoCAD (base) | 2025, 2026 | bridge runs; Civil 3D tools need Civil 3D objects |

## Supported MCP clients

Any client that speaks MCP over **stdio** works. Tested/validated:

- Claude Desktop (`examples/clients/claude-desktop.json`)
- VS Code MCP extension (`examples/clients/vscode-mcp.json`)
- Cursor (`examples/clients/cursor-mcp.json`)
- Cline (`examples/clients/cline-mcp.json`)

## Installation

See `docs/Installation.md` for the full guide. Summary:

1. **Install the bridge**: copy `Civil3D.Bridge.Bundle-1.0.0` from the release
   zip into `%APPDATA%\Autodesk\ApplicationPlugins\` and restart Civil 3D.
2. **Install the server**: `npm install -g autodesk-mcp-server` (or `npx
   autodesk-mcp-server`).
3. **Configure a client** with a stdio command pointing at the server binary
   (configs in `examples/clients/`).
4. Start Civil 3D, then start the client - tools are discovered automatically.

Verify the install: `autodesk-mcp-server --version` prints `1.0.0`.

## Architecture summary

```
AI client (Claude Desktop, Cursor, VS Code, Cline, ...)
        |  Model Context Protocol (stdio)
        v
Autodesk.MCP.Server   (TypeScript / Node.js - product-agnostic MCP server)
        |  JSON-RPC 2.0 over a local Windows named pipe (CurrentUserOnly ACL)
        v
Civil3D.Bridge        (C# / .NET 8 plugin loaded inside Civil 3D)
        |  Autodesk .NET API (in-process)
        v
Autodesk Civil 3D 2025 / 2026
```

Key design points: dynamic tool discovery (the server learns the catalog from the
bridge at handshake - nothing is hardcoded), FIFO-serialized execution on the
bridge for Autodesk thread safety, confirmation-gated editing, progress and
cancellation forwarding by correlation id, and automatic reconnect with bounded
backoff. See `docs/ARCHITECTURE.md`.

## Available capabilities

Read and edit Civil 3D drawings across:

- Alignments, profiles, surfaces, corridors, pipe networks, COGO points, styles
- Project summary, drawing health, design validation, quantity takeoff, surface
  comparison, corridor analysis, cut/fill calculation
- Query framework (filtering, sorting, pagination), editing tools with
  transaction safety and confirmation, engineering workflows, LandXML export

The full tool catalog is delivered by the bridge at runtime; run
`tools/list` against the server to see the exact catalog for the connected
Civil 3D version.

## Security / trust model

- Local-only: server <-> bridge traffic is over per-user Windows named pipes
  with a `CurrentUserOnly` security descriptor.
- No network exposure; no authentication introduced (local Windows user is the
  trust boundary).
- Raw Autodesk exceptions and stack traces never cross the protocol boundary;
  failures are mapped to stable error codes with safe messages.
- The server exposes no filesystem write surface.
- Final release sweep: no credentials, tokens, secrets or private paths in the
  repository, examples or release archives.

See `docs/PRODUCTION-HARDENING.md` section 6 for the full security review.

## Compatibility matrix

| Component | Versions |
| --- | --- |
| Civil 3D | 2025, 2026 |
| Bridge | 1.0.0 |
| Bridge protocol | 1.x (SemVer, major-checked at handshake) |
| MCP server | 1.0.0 |
| MCP clients | Claude Desktop, VS Code, Cursor, Cline (stdio) |
| Runtime | .NET 8 (Desktop) bridge; Node >= 20 server; Windows 10/11 x64 |

Forward compatibility: unknown protocol fields and unknown enum values are
tolerated; protocol major mismatches are refused with a structured error.

## Known limitations

- Bridge execution is FIFO-serialized by design (Autodesk thread safety); read
  workloads do not parallelize.
- Windows-only hosting (named pipes); no remote/network bridge access.
- Single tool-result payloads are bounded by the framing message-size limit.
- Confirmation UX depends on MCP client elicitation support; clients without it
  must pass `confirm: true` for editing tools.

## Performance characteristics

- Pipe throughput and startup/handshake/manifest-load benchmarks are in
  `docs/PERFORMANCE-BENCHMARKS.md`.
- Stress-verified: 1,000-tool manifests and 50 concurrent calls through the real
  stack settle within budget; scaling is linear (see
  `docs/PRODUCTION-HARDENING.md` section 4).

## Upgrade path from RC2

1. Stop the MCP server and close the client.
2. Install the new npm package: `npm install -g autodesk-mcp-server@1.0.0`.
3. Replace the bridge bundle folder in `%APPDATA%\Autodesk\ApplicationPlugins\`
   with the 1.0.0 bundle (remove the old `Civil3D.Bridge.Bundle-1.0.0-rc.2`
   folder) and restart Civil 3D.
4. Restart the client. Version is confirmed via `autodesk-mcp-server --version`.

No configuration or data migration is required between RC2 and 1.0.0.

## Rollback

To return to the previous release:

1. `npm install -g autodesk-mcp-server@1.0.0-rc.1` (the last previously
   published version; 1.0.0-rc.2 was validated but never published) and restart
   the client.
2. Restore the previous bridge bundle folder in
   `%APPDATA%\Autodesk\ApplicationPlugins\` and restart Civil 3D.
3. Confirm `--version` reports the rolled-back version and tools/list works.

Rollback is safe: no state is persisted by the server or bridge beyond the
endpoint registry (auto-rewritten on each bridge start) and log files.

---

See also: `docs/RELEASE-1.0.0-FINAL.md` (final release report) and
`docs/RELEASE-VALIDATION.md` (manual validation checklist).
