# 06 — Antipatterns

The specific ways AI output goes wrong in *this* repository. Each entry: the
symptom, why it happens, and the correction.

Use this file as a **diagnostic** — when output looks off, find the matching
entry and paste that entry back at the model with the offending code.

---

## AP-1 — Registering the tool by hand

**Symptom:** the model adds the new tool to a `switch`, a dictionary, a
`services.AddSingleton<MyTool>()` list, or a TypeScript tool array.

**Why:** every other codebase the model has seen requires registration.

**Correction:** `ToolCatalog` reflection-scans loaded assemblies. A class with
`[McpTool]` in a referenced assembly *is* registered. The only manual step ever
needed is a single `ProjectReference` when a brand-new tool assembly is created.
See rule A4.

---

## AP-2 — Autodesk types leaking upward

**Symptom:** `using Autodesk.AutoCAD…` appears in `Civil3D.Tools.Abstractions`,
`Autodesk.Mcp.Sdk`, `Autodesk.Mcp.Shared`, or a domain project. Or a tool class
opens a `Transaction` itself.

**Why:** the direct route is shorter, and the model optimizes for the fewest files.

**Correction:** define an Autodesk-free service contract in Abstractions; put the
real implementation in the tool assembly. That isolation is why every test runs
headless. Rules A3, A5; pattern P1.

---

## AP-3 — Teaching the server about Civil 3D

**Symptom:** a tool name, a Civil 3D concept, or a product-specific branch appears
in `src/server/`.

**Why:** the model fixes a symptom where it is visible rather than where it lives.

**Correction:** the server is product-agnostic by design — that is what lets a
future AutoCAD or Revit bridge plug in with zero server change. Push the fix into
the bridge or into the tool's manifest metadata. Rules A1, A7.

---

## AP-4 — Hand-written JSON Schema

**Symptom:** a `schema.json`, a schema literal in TypeScript, or a
`JsonSchema` attribute soup describing the tool inputs.

**Why:** most MCP examples hand-write schemas.

**Correction:** schemas are generated from the C# DTO types at startup. If the
schema is wrong, the DTO is wrong. Fix the DTO. Pattern P2.

---

## AP-5 — Happy-path-only tests

**Symptom:** one `[Fact]` that creates the thing and asserts `Success == true`.

**Why:** it satisfies "add tests" at minimum cost.

**Correction:** every tool needs failure coverage: no active document, validation
failure, unknown id, confirmation denied, cancellation. The failure paths are
where the error-code contract lives, and they are the ones clients depend on.
Rule D2.

---

## AP-6 — "Done!" without running anything

**Symptom:** the change is declared complete with no command output, or with
"tests should pass".

**Why:** models are rewarded for confident closure.

**Correction:** paste real output from [05-VERIFICATION.md](05-VERIFICATION.md),
and state the confidence level in the exact words defined there. A change that
compiles but was never run inside Civil 3D is **compiles only** — that is an
honest, acceptable outcome; a false "verified" is not.

---

## AP-7 — Scope creep disguised as helpfulness

**Symptom:** the diff renames things, reorders usings, reformats untouched
methods, "improves" logging, or adds a `docs/SOMETHING-SUMMARY.md` nobody asked
for.

**Why:** models pad output to look thorough.

**Correction:** the diff should contain only what the plan listed. Unrelated
changes make review expensive and bugs invisible. Rules E4, E6.

---

## AP-8 — Inventing a near-duplicate error code

**Symptom:** `E_NOT_FOUND` alongside the existing `E_OBJECT_NOT_FOUND`;
`E_INVALID_INPUT` alongside `E_VALIDATION_FAILED`.

**Why:** the model does not read the existing code list first.

**Correction:** grep the existing codes in `Autodesk.Mcp.Shared` and reuse.
Clients branch on these strings. Rule B4.

---

## AP-9 — Renaming a shipped tool or DTO field

**Symptom:** `create_pipe` becomes `pipe_create`, or `DiameterMm` becomes
`Diameter`, "for consistency".

**Why:** the model optimizes for internal tidiness over external compatibility.

**Correction:** shipped names are permanent. Rename = new tool + deprecation of
the old one. Rules B1, B2.

---

## AP-10 — Hand-editing version numbers

**Symptom:** `Directory.Build.props`, `package.json` or `PackageContents.xml`
edited directly; `npm run quality:check` then fails on drift.

**Correction:** edit `eng/version.json`, run `npm run sync:version`. Rule B5.

---

## AP-11 — Thrashing on a build error

**Symptom:** three or more attempts, each trying a different unrelated fix, the
diff growing with dead code and commented-out attempts.

**Why:** no diagnosis step; the model pattern-matches on the error string.

**Correction:** after two failed attempts, stop. Report the exact error, what was
tried, and what you believe the cause is. A human unblocks it in a minute; the
third guess costs an hour of cleanup.

---

## AP-12 — Silencing the compiler

**Symptom:** `!` null-forgiveness, `#pragma warning disable`, `// eslint-disable`,
or a cast added purely to make the build pass.

**Why:** warnings are errors here, so suppression is the fastest green.

**Correction:** the warning is the design telling you something. Fix the
nullability or the type. A suppression needs a one-line justification and is
reviewable. Rules C1, C2, C9.

---

## AP-13 — Mocking the Autodesk API in tests

**Symptom:** a test project sprouts mocks of `Document`, `Database`,
`Transaction`.

**Why:** the model reaches for a mocking framework by reflex.

**Correction:** use the existing harnesses (`EditingTestHarness`,
`InMemoryDrawing`). If the thing you want to test needs a real transaction, it is
not unit-testable — verify it manually in Civil 3D and say so. Rules D3, D4.

---

## AP-14 — Silent assumption instead of one question

**Symptom:** the model picks units, a default, or an error behaviour without
flagging it, and the choice is wrong.

**Correction:** one clarifying question at Phase 0 costs one message; a wrong
assumption costs the whole change. If you must proceed, state the assumption
explicitly at the top of the plan and again in the report.
