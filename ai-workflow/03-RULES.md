# 03 — Hard Rules

Every item here is a **defect** if violated, not a style preference. Paste this
file into every session.

---

## A. Architectural invariants (breaking one is a design failure)

**A1. `Autodesk.Mcp.Server` (TypeScript) must never know about Autodesk or Civil 3D.**
No tool name, no Civil 3D concept, no product-specific branch. The server learns
the catalog at handshake time. If a change requires the server to know a tool
exists, the change is wrong.

**A2. `Civil3D.Bridge` must never know about MCP or AI.**
It speaks a private JSON-RPC protocol. No MCP types, no prompt text, no client
awareness.

**A3. `Autodesk.Mcp.Shared`, `Autodesk.Mcp.Sdk` and `Civil3D.Tools.Abstractions`
must never reference an Autodesk assembly.**
This is what keeps tests headless. If you need an Autodesk type in one of these,
you need an interface instead.

**A4. Tools are never registered by hand.**
`ToolCatalog` reflection-scans loaded assemblies. Adding a tool = adding a class
with `[McpTool]`. If you find yourself editing a list of tools, stop.

**A5. Tools never call the Autodesk API directly.**
A tool depends on an Autodesk-free service contract (`ICivil3DSession`,
`IDrawingStatisticsService`, `ICreatePipeService`, …). The real implementation —
the only code touching Autodesk — lives beside it in the same tool assembly.

**A6. No network listeners, ever.** Local named pipes only, ACL'd to the current
user. Do not add an HTTP server, a socket, or telemetry egress.

**A7. Adding a new product bridge (AutoCAD, Revit, …) must require zero server
changes.** If your design breaks this, redesign it.

---

## B. Contracts and compatibility

**B1. A shipped tool name is permanent.** `create_pipe` stays `create_pipe`
forever. Rename = new tool + deprecation, never an in-place rename.

**B2. DTOs are the wire contract.** Adding a required field or removing a field
is a breaking change. Bump the tool's `Version` in `[McpTool]` on any schema or
behaviour change.

**B3. Protocol changes are dual-sided.** Any change to the bridge protocol must
land in **both** `Autodesk.Mcp.Shared` (C#) and
`src/server/Autodesk.Mcp.Server/src/protocol/` (TypeScript), with round-trip tests
on both sides, plus a protocol version bump (breaking = major).

**B4. Error codes are stable identifiers.** They live as an enum in
`src/shared/Autodesk.Mcp.Shared/Errors/ErrorCode.cs`. Reuse the member that fits;
adding one is a contract change, and inventing a near-duplicate (`E_NOT_FOUND`
next to `E_OBJECT_NOT_FOUND`) breaks client handling.

**B5. Never hand-edit a version number.** `eng/version.json` is the source;
`npm run sync:version` propagates it. `npm run quality:check` fails on drift.

---

## C. Code quality (enforced by the build)

**C1. Zero warnings.** `src/Directory.Build.props` sets
`TreatWarningsAsErrors=true` with latest analyzers. A warning fails the build.

**C2. Nullable reference types are on.** No `!` null-forgiveness to silence the
compiler; fix the nullability.

**C3. Public types and members carry XML docs** — including DTO properties.
Match the density of the file you are modelling on.

**C4. DTOs are immutable `record`s with `init`-only properties.**

**C5. Constructor injection only.** Never `new` up a dependency inside a tool or
handler.

**C6. Raw exceptions never cross the pipe.** Map to `BridgeException` with a
stable code. The base class maps anything unexpected to `E_INTERNAL` — rely on
that rather than swallowing exceptions yourself.

**C7. Read paths use read-only transactions.** Write paths go through the command
pipeline with a document lock, validation, confirmation, commit/rollback.

**C8. `dotnet format` must be clean** — CI runs
`dotnet format AutodeskMcp.Core.slnx --verify-no-changes`.

**C9. TypeScript must pass `typecheck`, `lint` and `test`.** No `any` added to
existing typed code; no `// eslint-disable` without a one-line justification.

---

## D. Testing

**D1. Every behaviour change ships with tests in the same change.**

**D2. Failure modes are tested, not just the happy path.** For a tool: missing
document, validation failure, unknown id, cancellation.

**D3. Tests run headless.** No test may require a running Civil 3D. Use the
in-memory harnesses (`EditingTestHarness`, `InMemoryDrawing`, …).

**D4. Tests assert behaviour, not implementation.** Assert on the returned DTO
and raised events, not on how many times a mock was called.

**D5. Never delete or `[Skip]` a failing test to go green.** Report it.

---

## E. Process

**E1. No plan, no code.** See [02-WORKFLOW.md](02-WORKFLOW.md).

**E2. One logical change at a time.**

**E3. No new third-party dependency without explicit approval.**

**E4. No unrequested refactoring, renaming, or reformatting.**

**E5. Never claim a change is verified against Civil 3D unless it actually ran
inside Civil 3D.** State the real confidence level.

**E6. Do not create files outside the plan** — especially not new top-level
directories, new docs, or "summary" markdown files nobody asked for.

**E7. Scratch output goes in `tools_tmp/` or a temp directory**, never into
`src/`, `docs/`, or the repo root.
