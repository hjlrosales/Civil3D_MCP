# Developer Guide

How to build, test, benchmark and release the platform from a source checkout.

---

## 1. Prerequisites

| Tool | Version | Notes |
| --- | --- | --- |
| .NET SDK | 8.0+ (10.0 recommended) | builds all C# projects |
| Node.js | >= 20 | builds the server |
| Windows | 10/11 | named pipes; required for pipe tests |
| Autodesk SDK | AutoCAD 2025 install | only for `Civil3D.Bridge` and `Civil3D.Tools.Drawing` |

Only `Civil3D.Bridge` and `Civil3D.Tools.Drawing` reference Autodesk assemblies.
Everything else (Shared, Sdk, Domain, most Tools) builds and tests without the SDK.

---

## 2. Repository layout

```
src/shared/Autodesk.Mcp.Shared   protocol contracts, envelopes, error codes (no Autodesk)
src/sdk/Autodesk.Mcp.Sdk         bridge infrastructure: pipe host, router, discovery (no Autodesk)
src/domain/*                     Civil 3D domain services (thin API wrappers)
src/bridges/*                    tool libraries + Civil3D.Bridge plugin (Autodesk refs only in Bridge/Drawing)
src/server/Autodesk.Mcp.Server   TypeScript MCP server
eng/                             versioning + release scripts (version.json is the source of truth)
packaging/                       bundle template (PackageContents.xml)
benchmarks/                      .NET + Node benchmark suites
 e2e/                            real-process end-to-end tests
examples/                        sample configs, client configs, prompts, JSON-RPC
```

---

## 3. Build

```bash
# .NET (everything except the Autodesk-dependent projects)
dotnet build src/shared/Autodesk.Mcp.Shared/Autodesk.Mcp.Shared.csproj -c Release
dotnet build src/sdk/Autodesk.Mcp.Sdk/Autodesk.Mcp.Sdk.csproj -c Release

# Bridge (requires the Autodesk SDK; default path C:\Program Files\Autodesk\AutoCAD 2025)
dotnet build src/bridges/Civil3D.Bridge/Civil3D.Bridge.csproj -c Release
# custom SDK path:
dotnet build src/bridges/Civil3D.Bridge/Civil3D.Bridge.csproj -p:AutodeskAcadDir="C:\Program Files\Autodesk\AutoCAD 2026"

# Server
npm --prefix src/server/Autodesk.Mcp.Server install
npm --prefix src/server/Autodesk.Mcp.Server run build
```

Or run the whole thing with the orchestrator:

```bash
npm run build:server
npm run build:bridge          # builds + assembles the bundle (requires Autodesk SDK)
```

---

## 4. Test

```bash
# .NET unit/integration tests (no Autodesk needed):
dotnet test tests/Autodesk.Mcp.Shared.Tests -c Release
dotnet test tests/Autodesk.Mcp.Sdk.Tests -c Release
dotnet test tests/Civil3D.Domain.Tests -c Release
# ... any test project except those referencing Civil3D.Bridge

# Bridge-dependent tests (require the Autodesk SDK):
dotnet test tests/Civil3D.Tools.Drawing.Tests -c Release

# Server unit + integration (named pipes, real pipe server in tests):
npm --prefix src/server/Autodesk.Mcp.Server test

# End-to-end (spawns the real server binary over stdio MCP):
npm run build:server && npm run test:e2e

# Formatting check:
dotnet format AutodeskMcp.slnx --verify-no-changes --no-restore
```

---

## 5. Lint / static analysis

- .NET: warnings-as-errors + analyzers enforced by `src/Directory.Build.props`.
- Server: `npm --prefix src/server/Autodesk.Mcp.Server run lint` (ESLint,
  typescript-eslint recommended, zero warnings allowed).
- TypeScript: `npm --prefix src/server/Autodesk.Mcp.Server run typecheck` (strict).

---

## 6. Benchmarks

```bash
# .NET protocol/pipe benchmarks (Release)
npm run bench:dotnet

# Node server benchmarks (spawns the real server process)
npm run bench:server
```

See `docs/PERFORMANCE-BENCHMARKS.md` for the metric list and methodology.

---

## 7. Quality gate (what CI runs)

```bash
npm run quality          # full local gate: typecheck, lint, test, build, pack, bundle
npm run quality:check    # verify-only mode (no installs/builds)
```

The gate mirrors `.github/workflows/ci.yml`: restore -> build -> test -> lint ->
format -> package. On machines without the Autodesk SDK the bridge step is skipped
with a note.

---

## 8. Version bumps

1. Edit `eng/version.json` (or pass the version to the sync script):
   ```bash
   node eng/scripts/sync-version.mjs 1.0.0-rc.2
   ```
2. The script rewrites `Directory.Build.props`, both `package.json` files,
   `PackageContents.xml` and the sample/shipped config versions.
3. Add a `CHANGELOG.md` entry under the new version.

---

## 9. CI/CD notes

- Cloud CI (`.github/workflows/ci.yml`) builds/tests everything that does not need
  the Autodesk SDK on `windows-latest`.
- The bridge build + bundle assembly runs on a self-hosted Windows runner labeled
  `civil3d` (registered on a machine with Civil 3D installed), or locally via
  `npm run build:bridge`.
- `release.yml` publishes on `v*` tags: npm publish (guarded by the `NPM_TOKEN`
  secret) and a GitHub Release with the bridge bundle + npm tarball attached.

---

## 10. Architecture cheat sheet

- The Bridge speaks a private JSON-RPC/NDJSON protocol over a local named pipe; it
  has no MCP or AI knowledge.
- The Server has no Autodesk references; it discovers the tool catalog at handshake
  (`tools/list`) and registers every tool dynamically as MCP tools.
- Shared wire contracts live in `Autodesk.Mcp.Shared` (C#); the server mirrors the
  small stable protocol layer in `src/protocol/` with round-trip tests on both sides.
- Editing tools run inside a `DocumentLock` + transaction with rollback and stable
  `errorCode`s; raw exceptions never cross the pipe.

See `docs/ARCHITECTURE.md` for the full design.
