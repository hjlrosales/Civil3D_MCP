# 01 — Project Map

Read this before proposing any change. It answers "where does my code go?"

---

## 1. Mental model in one paragraph

An AI client talks **MCP over stdio** to a TypeScript server. The server talks
**JSON-RPC 2.0 over a local Windows named pipe** to a C# plugin loaded inside
Civil 3D. The server has no Autodesk knowledge — it learns the entire tool
catalog from the bridge at handshake time. The bridge has no MCP knowledge — it
is an execution engine. Tools live in the bridge, are found by reflection, and
adding one requires editing **no** registry, **no** server file, and **no**
schema file.

```
AI client ── MCP/stdio ──▶ Autodesk.Mcp.Server (TS) ── JSON-RPC/named pipe ──▶ Civil3D.Bridge (C#) ──▶ Civil 3D
```

---

## 2. Directory map

```
src/
  shared/   Autodesk.Mcp.Shared        Wire contracts, error codes, enums, JSON options.
                                       Referenced by everything. Mirrored in TS.
  sdk/      Autodesk.Mcp.Sdk           Tool base classes, [McpTool] attribute,
                                       ToolCatalog (reflection scan), schema generation.
  domain/   Civil3D.Domain*            Autodesk-free domain: entities, queries, commands,
                                       workflows, validation. Unit-testable headless.
  bridges/  Civil3D.Bridge             The host: plugin entry point, DI root, dispatcher,
                                       application-context marshalling. NO tools live here.
            Civil3D.Tools.Abstractions Autodesk-free contracts shared by every tool assembly
                                       (Civil3DToolBase, ICivil3DSession, ActiveDrawing…).
            Civil3D.Tools.<Domain>     The tools themselves + the real Autodesk service
                                       implementations. One assembly per domain:
                                       Drawing, Query, Surface, Corridor, CutFill, Editing,
                                       Export, Health, Project, Quantity, Validation,
                                       Workflows, Commands.
  server/   Autodesk.Mcp.Server        TypeScript MCP server (npm package
                                       `autodesk-mcp-server`).
tests/                                 xUnit projects, one per src assembly.
e2e/                                   Vitest suites that spawn the real server binary.
benchmarks/                            BenchmarkDotNet + vitest bench.
eng/scripts/                           Version sync, quality gate, bundle build, release.
packaging/                             Autodesk bundle layout (PackageContents.xml).
docs/                                  Product documentation.
examples/                              Client configs and sample configuration.
tools_tmp/                             Scratch dumps from manual investigation. Not a
                                       source directory — do not import from it.
```

### Inside the TypeScript server

```
src/server/Autodesk.Mcp.Server/src/
  index.ts              Entry point / CLI.
  manager.ts            Owns bridge connections and the merged tool catalog.
  config.ts, logger.ts
  protocol/             constants.ts, types.ts, version.ts — the TS mirror of
                        Autodesk.Mcp.Shared. Changes here must be mirrored in C#.
  transport/            pipe.ts, ndjson.ts, bridgeConnection.ts — framing and reconnect.
  bridge/bridgeClient.ts  JSON-RPC client for the bridge protocol.
  discovery/            endpointStore.ts, monitor.ts — the %LOCALAPPDATA% endpoint registry.
  mcp/                  mcpAdapter.ts, schema.ts, errors.ts — MCP surface + validation.
```

---

## 3. Dependency direction (enforced — cycles are a build failure)

```
Civil3D.Bridge
   └─▶ Civil3D.Tools.<Domain>
          └─▶ Civil3D.Tools.Abstractions
                 └─▶ Autodesk.Mcp.Sdk
                        └─▶ Autodesk.Mcp.Shared
Civil3D.Tools.<Domain> ─▶ Civil3D.Domain.<Area>  (Autodesk-free)
```

Nothing points back up. `Autodesk.Mcp.Shared` and `Autodesk.Mcp.Sdk` never
reference Autodesk assemblies. `Civil3D.Tools.Abstractions` never references
Autodesk assemblies — that is what keeps every tool test runnable headless.

---

## 4. Which solution to build

| Solution | Contains | Needs Civil 3D installed? |
| --- | --- | --- |
| `AutodeskMcp.Core.slnx` | Everything that builds without the Autodesk SDK | No |
| `AutodeskMcp.slnx` | Full solution including `Civil3D.Bridge` and tool assemblies | Yes |

CI runs `Core` on every PR; the full solution only runs on version tags, on a
self-hosted runner that has Civil 3D. **If you cannot build `AutodeskMcp.slnx`
locally, say so in your report rather than claiming the change is verified.**

---

## 5. Frameworks and versions

- C#: .NET 8 target for the bridge (`net8.0-windows`), .NET 10 SDK to build;
  test projects target `net10.0-windows`. xUnit 2.9.
- TypeScript: Node ≥ 20 (CI uses 22), Vitest, ESLint, `tsc` typecheck.
- Warnings are **errors** for everything under `src/` (`src/Directory.Build.props`).
- Versions come from `eng/version.json` and are propagated by
  `node eng/scripts/sync-version.mjs`. Never hand-edit a version number.

---

## 6. The runtime facts that change how you write code

- **Autodesk APIs are single-threaded.** Every tool runs on the application
  context via the dispatcher. Tools never touch `Application.*` directly.
- **Editing runs in a transaction** with a document lock: open → validate →
  execute → commit, rollback + structured error on failure.
- **Raw exceptions never cross the pipe.** Everything becomes a `BridgeException`
  with a stable `errorCode` (`E_NO_ACTIVE_DOCUMENT`, `E_VALIDATION_FAILED`,
  `E_OBJECT_NOT_FOUND`, `E_TRANSACTION_FAILED`, `E_INTERNAL`, …).
- **JSON Schemas are generated from C# DTOs at startup.** There is no
  hand-written schema anywhere. If a schema looks wrong, fix the DTO.
- **Bridge discovery is a file registry**, not a fixed pipe name:
  `%LOCALAPPDATA%\AutodeskMcp\endpoints\<product>-<pid>.json`.

---

## 7. Reference docs, by subject

| Subject | Doc |
| --- | --- |
| Whole system, ADRs, sequence diagrams | `docs/ARCHITECTURE.md` |
| Building a tool (the contract) | `docs/TOOL-DEVELOPMENT.md` |
| Write pipeline: validation, confirmation, events, undo | `docs/COMMAND-FRAMEWORK.md` |
| Existing editing tools | `docs/EDITING-TOOLS.md` |
| Domain layer design | `docs/DOMAIN-LAYER.md` |
| Query framework | `docs/QUERY-FRAMEWORK.md` |
| Workflow framework | `docs/WORKFLOW-FRAMEWORK.md` |
| TypeScript server internals | `docs/MCP-SERVER.md` |
| Build/test/bench from source | `docs/DeveloperGuide.md` |
| Release mechanics | `docs/ReleaseProcess.md` |
| Hardening, limits, failure modes | `docs/PRODUCTION-HARDENING.md` |
