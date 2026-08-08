# Final Release Report - Autodesk MCP Platform 1.0.0

## Release status

**GA / Production Release** - v1.0.0

- Git tag: `v1.0.0` (see `git rev-parse v1.0.0^{}` for the release commit)
- Date: 2026-08-08
- Promoted from: 1.0.0-rc.2 (RC2, Phase 9 hardening) with version
  synchronization and release-artifact cleanup only.

---

## Validation

### Test counts

| Suite | Count | Result |
| --- | --- | --- |
| .NET tests (core + bridge) | 526 | PASS |
| Server / TypeScript tests | 99 (16 files, incl. 8 hardening suites) | PASS |
| End-to-end (real server process) | 13 (7 suites) | PASS |
| Hardening suites (failure/recovery, concurrency, security, compat, leaks, stress, diagnostics) | PASS |

### Quality gate

All stages PASS on the 1.0.0 build:

- version drift check
- npm ci, typecheck, lint, test, build, pack (server)
- fresh-install validation
- .NET core build + test + format (verify-no-changes)
- .NET full build (incl. bridge) + full test
- bridge bundle
- e2e suite

Zero warnings, zero errors.

### CI/CD run results (actual)

The official release was produced by the **Release** workflow (`release.yml`) on the
`v1.0.0` tag (commit `19f36d3`):

| Job | Result |
| --- | --- |
| Node (server) - build & pack | PASS |
| .NET (core) | PASS |
| Bridge bundle (Civil 3D SDK runner) | PASS |
| E2E (real server process) | PASS |
| Publish to npm | PASS (publish skipped - see note below) |
| GitHub Release | PASS |

Run: `31259233823` (green) on commit `19f36d3`. The **GitHub Release** was
created successfully and is live at `v1.0.0` (published 2026-08-08, not a
prerelease) with all four assets: `autodesk-mcp-server-1.0.0.tgz`,
`Civil3D.Bridge.Bundle-1.0.0.zip`, `release-manifest.json` and `SHA256SUMS`.

> **Note**: an earlier push-triggered **CI** run (`31261449860`, same commit)
> failed only in the `fresh-install` step of the Node job due to a missing
> closing quote in `ci.yml` (`--tarball="$TARBALL`). This was a workflow-script
> typo, not a product defect; it has been fixed (the quote is now closed) so
> subsequent push CI runs pass. The release pipeline itself was unaffected.

### Packaging results

- `autodesk-mcp-server-1.0.0.tgz` - npm pack succeeds; contains only compiled
  JS, `.d.ts` declarations, README and package.json (no source maps, no tests,
  no source files). CLI verified: `--version` prints `1.0.0`, `--help` works.
- `Civil3D.Bridge.Bundle-1.0.0.zip` - bundle assembles cleanly; contains
  `PackageContents.xml` (AppVersion 1.0.0, Civil 3D 2025/2026
  R24.3-R25.0), 54 managed DLLs, `Configuration/bridge.config.json` and the
  runtime `deps.json`. No `.pdb`, no XML doc files, no test assemblies, no
  debug artifacts.

### Fresh-install results

`eng/scripts/validate-fresh-install.mjs --server` against the 1.0.0 tarball:

1. Use pre-packed tarball - PASS
2. npm install into a clean temp dir - PASS
3. Installed CLI `--version` reports `1.0.0` - PASS
4. Installed CLI `--help` lists config flag - PASS
5. Installed CLI starts to the ready state (empty endpoint registry) - PASS

Bundle zip integrity (opens cleanly, contains PackageContents.xml with the
correct AppVersion) - PASS.

> **Manual validation pending**: the live-Civil-3D steps of the installation
> validation (bundle install into `%APPDATA%\Autodesk\ApplicationPlugins`,
> Civil 3D 2025/2026 start, endpoint registration, handshake, tools/list, and
> representative read/edit/workflow execution) require a licensed Civil 3D host
> and are executed per `docs/RELEASE-VALIDATION.md` - they are **not** part of
> the automated gate.

### E2E results

All 13 end-to-end tests pass: startup, handshake, discovery, execution,
failures, confirmation flow, progress forwarding, cancellation, reconnect,
shutdown, multi-instance bridge selection.

---

## Artifacts

| Artifact | Version | SHA-256 | Size (bytes) |
| --- | --- | --- | --- |
| Civil3D.Bridge.Bundle-1.0.0.zip | 1.0.0 | `5f469e6c436beb572e4e252c2c0d06de5a1d5bcf8156ffaaf4091dad825eca19` | 3,964,467 |
| autodesk-mcp-server-1.0.0.tgz | 1.0.0 | `df0822bda78f9e4c62bbe767c304fdede099e4e7abbc63152d38194c883dec69` | 28,731 |

These are the hashes of the artifacts actually attached to the GitHub Release
(verified from the released `SHA256SUMS`). Machine-readable manifest:
`artifacts/release-manifest.json` (includes build UTC timestamps and platform
`win32 x64`). Checksums: `artifacts/SHA256SUMS`. The `release.yml` workflow
attaches both to the GitHub Release together with the artifacts and release notes.

---

## Compatibility

| Component | Versions |
| --- | --- |
| Civil 3D | 2025, 2026 |
| Bridge | 1.0.0 |
| Bridge protocol | 1.x (SemVer, major-checked) |
| MCP server | 1.0.0 |
| Node.js (server) | >= 20 |
| MCP clients | Claude Desktop, VS Code, Cursor, Cline (stdio) |
| OS | Windows 10/11 x64 (named pipes) |

## Known limitations

- Bridge tool execution is FIFO-serialized by design (Autodesk thread safety).
- Windows-only hosting; no remote/network bridge access (local security
  boundary).
- Single tool-result payloads bounded by the framing message-size limit.
- Confirmation UX depends on MCP client elicitation support (otherwise pass
  `confirm: true`).

## Installation

Full guide: `docs/Installation.md`. Release notes (incl. upgrade path from RC2
and rollback): `docs/RELEASE-1.0.0.md`. Quick start: `docs/QuickStart.md`.

## Rollback

To return to the previous release, reinstall the previous npm version
(`npm install -g autodesk-mcp-server@1.0.0-rc.1`, the last previously published
version - rc.2 was validated but never published) and restore the previous
bridge bundle folder in `%APPDATA%\Autodesk\ApplicationPlugins\`. No state is
persisted by the server or bridge beyond the auto-regenerated endpoint registry
and log files, so rollback is safe. See `docs/RELEASE-1.0.0.md` (Rollback).

## Release recommendation

**The implementation is ready for production.** All quality gates pass, all
release artifacts are validated and clean, the security sweep found no
credentials/tokens/secrets, the final regression suite passes with zero
warnings and zero errors, and the release pipeline (`release.yml`) has produced
the GitHub Release with artifacts, manifest and checksums from the `v1.0.0` tag
(run `31259233823`, all jobs green).

### Manual step: npm registry publish

The npm **registry** publish is the one remaining manual step. `NPM_TOKEN` is
not configured as a repository secret, so the `Publish to npm` job skips the
actual publish (it logs "NPM_TOKEN not configured - skipping npm publish" and
exits 0). The publishable tarball `autodesk-mcp-server-1.0.0.tgz` is attached to
the GitHub Release, so the artifact is complete, but `autodesk-mcp-server@1.0.0`
is **not yet on the npm registry** (registry lookup returns 404).

To publish: add an npm automation/granular (read + write) access token as the
`NPM_TOKEN` Actions secret on the repository, then re-run the `Release`
workflow. The publish job will publish automatically; the idempotency guard
(`npm view autodesk-mcp-server@$VERSION`) prevents duplicate publishes, and the
publish step also requests npm provenance (`--provenance`).

---

*End of final release report. v1.0.0 is the official production release of the
Autodesk MCP Platform.*
