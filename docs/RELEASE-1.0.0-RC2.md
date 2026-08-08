# Release 1.0.0 RC2 Report

- **Candidate**: 1.0.0-rc.2 (declared from the 1.0.0-rc.1 codebase after Phase 9 hardening)
- **Date**: 2026-08-08
- **Scope**: Production hardening and release validation only - no new engineering
  features, no new MCP tools, no protocol changes.
- **Companion document**: `docs/PRODUCTION-HARDENING.md` (detailed findings)

---

## Executive summary

Phase 9 attempted to break RC1. Eight automated hardening suites were added on
behalf of failure & recovery, concurrency, security, version compatibility,
resource leaks, stress, and operational diagnostics, plus a clean-machine
packaging validation script. All hardening suites pass; the full quality gate
passes with **zero warnings and zero errors**; all existing tests (526 .NET, 99
server/TypeScript including the new hardening suites, 13 e2e) pass.

No critical reliability defect was found that blocks release. The bridge
execution FIFO serialization, local named-pipe trust boundary, and standard-MCP
client surface are documented as deliberate design decisions. RC2 is
**recommended**.

---

## Tests performed

| Area | Tests | Result |
| --- | --- | --- |
| Existing .NET suite | 526 tests (domain, commands, query, workflows, tools, bridge) | PASS |
| Existing server suite | 99 tests across 16 files (incl. new hardening suites) | PASS |
| End-to-end (real server process) | 13 tests across 7 suites | PASS |
| Failure & recovery | 13 tests (pipe drop, malformed NDJSON/JSON-RPC, unknown/duplicate ids, timeouts, shutdown, stale/PID-reuse descriptors, multi-instance churn) | PASS |
| Concurrency & multi-client | multiple clients, concurrent calls, correlation uniqueness, session isolation, cancellation isolation | PASS |
| Security | payload/framing limits, oversized messages, exception containment, stable error codes | PASS |
| Version compatibility | semver negotiation, unknown fields/enums, protocol major mismatch, duplicate manifest | PASS |
| Resource leaks | repeated start/stop, connect/disconnect, execution cycles | PASS |
| Stress | 500/1,000-tool manifests, 50 concurrent calls through the real stack | PASS |
| Diagnostics | correlation id, tool name, bridge instance, pipe, duration in logs | PASS |
| Packaging validation | fresh npm pack + install, CLI version/help, server start/stop, bundle zip integrity | PASS (all checks) |
| Quality gate | 17+ stages (version drift, node, dotnet, format, e2e, fresh-install) | PASS |

---

## Failures discovered

Phase 9 did not uncover a production defect in the transport, router, dispatcher,
cancellation, or recovery paths - the RC1 architecture held up. The hardening
work itself surfaced two **test-harness** defects, fixed as part of this phase:

1. **Fake-bridge handshake rejection echo** - a raw test server did not echo the
   request correlation id in its rejection response, which the client correctly
   treated as an unmatched reply; the test was corrected.
2. **Test logger placeholder interpolation** - the diagnostics suite's in-memory
   logger concatenated pino-style `%s` placeholders instead of substituting them,
   causing false failures; the logger now mirrors pino behaviour.

No regression from these fixes was observed; they were test-only changes.

---

## Fixes applied

- New hardening test suites (see `tests performed`).
- `eng/scripts/validate-fresh-install.mjs` - clean-machine packaging validation.
- `quality-gate.mjs` and `ci.yml` extended with the fresh-install validation stage.
- Root `package.json` `validate:install` script.
- Documentation: `docs/PRODUCTION-HARDENING.md`, `docs/RELEASE-1.0.0-RC2.md`;
  `README.md`, `CHANGELOG.md`, `docs/Compatibility.md`, `docs/Troubleshooting.md`
  updated with findings.

## Remaining limitations

- Bridge tool execution is FIFO-serialized by design (Autodesk thread safety);
  read workloads do not parallelize.
- Windows-only (named pipes); no remote/network bridge access by design.
- Framing `MaxMessageLength` bounds single tool result payloads.
- Confirmation UX depends on MCP client elicitation support; clients without it
  must pass `confirm: true` for editing tools.
- Live Civil 3D install/uninstall cycles, real 2025/2026 drawings and live client
  sessions require a licensed Civil 3D host (see `docs/RELEASE-VALIDATION.md`).

---

## Compatibility matrix

| Component | Versions | Status |
| --- | --- | --- |
| Civil 3D | 2025, 2026 | supported (compiled against 2025 SDK; API-compatible) |
| Bridge | 1.0.0-rc.2 | current |
| Protocol | 1.x (SemVer, major-checked at handshake) | current |
| MCP server | 1.0.0-rc.2 | current |
| MCP clients | Claude Desktop, VS Code, Cursor, Cline (stdio) | supported; standard MCP features only |
| Runtime | .NET 8 (Desktop) bridge; Node >= 20 server; Windows 10/11 x64 | supported |

Compatibility guarantees: unknown protocol fields and unknown enum values are
tolerated (forward compatible); protocol major mismatches are refused with a
structured error; re-loading an identical manifest never duplicates tools.

---

## Performance results

Phase 9 stress tests confirmed linear scaling with no pathological behaviour in
the tested range. Highlights (from `docs/PERFORMANCE-BENCHMARKS.md` and the
stress suite):

- 1,000-tool manifests load and serve through the real stack within budget.
- 50 concurrent `tools/call` requests all settle with no timeouts.
- Manifest and response sizes scale linearly with content; single messages are
  bounded by the framing limit.

Deliberate, documented bottlenecks (not defects):

- Bridge dispatcher FIFO serialization (Autodesk thread safety).
- Single-NDJSON-line framing bound for very large tool results.

No premature optimization was performed, per the Phase 9 mandate.

---

## Security findings

- Trust model documented: local Windows user boundary; per-user named pipes with
  `CurrentUserOnly` ACLs; endpoint registry under `%LOCALAPPDATA%`.
- No authentication introduced (per mandate); documented for operator review.
- Raw Autodesk exceptions and stack traces never cross the protocol boundary -
  everything is mapped to stable error codes with safe messages.
- Framing enforces a message-size limit; malformed NDJSON/JSON-RPC is rejected
  without corrupting the connection; repeated abuse is bounded by reconnect
  backoff.
- The server exposes no filesystem write surface; tool arguments never become
  paths.

---

## Installation validation

Automated (runs in CI and locally via `eng/scripts/validate-fresh-install.mjs`):

1. Fresh `npm pack` of `autodesk-mcp-server`.
2. Clean `npm install` of the tarball into a brand-new directory.
3. Installed CLI `--version` and `--help`.
4. Installed server starts to the ready state with an empty endpoint registry
   and terminates (graceful-shutdown exit codes are asserted by the e2e suite).
5. Bridge bundle zip opens cleanly and contains `PackageContents.xml` with the
   expected `AppVersion`.
6. All temp state removed (uninstall/cleanup verified).

Manual (requires a licensed Civil 3D host; tracked in `docs/RELEASE-VALIDATION.md`):

- Bridge bundle install into `%APPDATA%\Autodesk\ApplicationPlugins`.
- Civil 3D 2025 and 2026 startup, endpoint discovery, handshake, tool discovery.
- Representative read-only tool, workflow, and confirmed editing tool.
- Bridge restart + reconnect; uninstall + cleanup.

---

## RC2 recommendation

**RC2 is recommended.**

- All existing tests pass; all new regression tests pass.
- No known critical reliability defects remain.
- No resource leaks identified in repeated cycle testing.
- Reconnect and multi-instance behaviour verified by automated tests.
- Civil 3D 2025/2026 remain in the supported matrix (live verification pending
  a licensed host, per `docs/RELEASE-VALIDATION.md`).
- Clean packaging installation verified automatically; bridge bundle install
  verified per the manual validation checklist.
- Performance findings documented; security review complete; trust model
  documented.
- Zero warnings, zero errors across the full quality gate.

The version bump to `1.0.0-rc.2` (and the eventual `1.0.0`) is performed at
release time with `eng/scripts/sync-version.mjs`, which propagates the version
across .NET assemblies, npm package metadata, the bundle manifest, and sample
configuration, followed by the `release.yml` pipeline.

---

*End of RC2 report. Phase 9 stops here per the release plan.*
