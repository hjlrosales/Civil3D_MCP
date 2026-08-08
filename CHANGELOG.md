# Changelog

All notable changes to the Autodesk MCP Platform are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Version tags: `v<major>.<minor>.<patch>[-<prerelease>]` (for example `v1.0.0-rc.1`).

## [Unreleased]

## [1.0.0] - 2026-08-08

### Changed

- **Official production release.** Promoted from 1.0.0-rc.2 to 1.0.0 after the
  Phase 9 production-hardening work and final release validation. Version
  synchronized across .NET assemblies, npm package metadata, bundle manifest and
  sample configuration via `eng/scripts/sync-version.mjs`.
- Version synchronization single source of truth is `eng/version.json`.

### Security

- Final release sweep confirmed: no credentials, tokens or secrets in the
  repository, examples or logs; no private filesystem paths in documentation;
  no stack traces cross the MCP protocol.

## [1.0.0-rc.2] - 2026-08-08

> Declared by the Phase 9 hardening report (`docs/RELEASE-1.0.0-RC2.md`). The
> version bump to 1.0.0-rc.2 was applied at release time with
> `eng/scripts/sync-version.mjs`; the 1.0.0 release supersedes this entry.

### Added

- **Production hardening test suites** (`src/server/Autodesk.Mcp.Server/test/`)
  - Failure & recovery: pipe drop, malformed NDJSON/JSON-RPC, unknown and
    duplicate request ids, request/tool timeouts, shutdown during execution,
    stale/PID-reuse endpoint descriptors and multi-instance churn.
  - Concurrency & multi-client: multiple MCP clients against one bridge,
    concurrent calls, correlation-id uniqueness, session isolation, and
    request-scoped cancellation isolation.
  - Security: payload/framing limits, oversized messages, exception containment
    and stable error codes across the protocol boundary.
  - Version compatibility: semver negotiation, unknown fields/enum values,
    protocol major mismatch rejection and duplicate-manifest idempotence.
  - Resource leaks: repeated start/stop, connect/disconnect and execution
    cycles with no lingering requests or state.
  - Stress: large manifests (500/1,000 tools) and concurrent load through the
    real server stack.
  - Diagnostics: correlation id, tool name, bridge instance, pipe name and
    execution duration present in operator logs.
- **Packaging validation** (`eng/scripts/validate-fresh-install.mjs`):
  clean-machine npm pack + install, CLI `--version`/`--help`, server
  start/stop with an empty endpoint registry, and bundle zip integrity;
  wired into `quality-gate.mjs` and `ci.yml`.
- **Documentation** (`docs/PRODUCTION-HARDENING.md`): test strategy, failure
  scenarios, recovery behavior, concurrency model, performance findings,
  resource management, security/trust model, compatibility matrix, packaging
  validation, client compatibility and operational diagnostics.

### Fixed

- Installed-CLI help text now verified on stderr (usage convention; stdout stays
  clean for the protocol) in the fresh-install validation.

## [1.0.0-rc.1] - 2026-08-08

### Added

- **Packaging**
  - Civil3D.Bridge now ships as an Autodesk Application Bundle (`Civil3D.Bridge.Bundle`)
    with a `PackageContents.xml` auto-load manifest and release folder layout.
  - Autodesk.MCP.Server now packs as a publishable npm package (`autodesk-mcp-server`)
    with a production build, executable CLI (`--version`, `--help`, `--config`) and
    version metadata synchronized with the Bridge.
- **Installer & configuration**
  - Bridge bundle install/uninstall/upgrade guidance (per-user `ApplicationPlugins`).
  - npm install, `npx` and upgrade/uninstall guidance for the server.
  - Sample configuration: `bridge.config.json`, `server.config.json`, environment
    variable examples, logging examples and multi-bridge/multi-instance examples.
- **Documentation** (`docs/`)
  - Installation, QuickStart, Configuration, Troubleshooting, DeveloperGuide,
    Contributing, ReleaseProcess, Compatibility, FAQ, Performance/Benchmarks and
    Release Validation guides; Architecture document updated for Phase 8.
- **Examples** (`examples/`)
  - Client configurations for Claude Desktop, VS Code, Cursor and Cline.
  - Typical MCP prompts, end-to-end workflow transcripts and JSON-RPC wire examples.
- **CI/CD** (`.github/workflows/`)
  - `ci.yml` quality gates (restore, build, test, lint, format, package) for .NET and Node.
  - `release.yml` publishing pipeline (npm publish, bridge bundle, GitHub Release).
- **Versioning & release automation**
  - `eng/version.json` single source of truth with `sync-version.mjs` synchronization
    across .NET assemblies, npm package and bundle manifest.
  - `CHANGELOG.md` and `release-notes.mjs` release-note generation.
- **Benchmarks**
  - .NET benchmark harness (`benchmarks/Autodesk.Mcp.Benchmarks`): protocol
    serialization, manifest generation, named-pipe throughput, reconnect latency,
    startup and memory usage.
  - Node benchmark suite (vitest bench): handshake, tool discovery, large-manifest
    loading, workflow-style execution, reconnect latency and memory.
- **End-to-end tests** (`e2e/`)
  - Real-process suite driving the built server over stdio MCP against a
    protocol-faithful fake bridge: startup, handshake, discovery, execution,
    cancellation, progress, confirmation, reconnect, shutdown and multi-instance
    bridge selection.

### Fixed

- Bridge configuration binding: `bridge.config.json` is now read from its `"bridge"`
  section, matching the documented loader (`GetSection("bridge")`), so shipped
  configuration values actually take effect.

### Changed

- Root `Directory.Build.props` centralizes version and repository metadata for all
  C# projects; `src/Directory.Build.props` enforces warnings-as-errors and analyzers.
- The server now reads its own version from `package.json` at runtime and reports it
  via `--version` and in the MCP `initialize` server info.

[1.0.0-rc.1]: https://github.com/your-org/autodesk-mcp-platform/releases/tag/v1.0.0-rc.1
