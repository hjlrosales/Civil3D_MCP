# Autodesk MCP Platform — Drawing Health Report (Phase 7B)

**Status:** Implemented
**Date:** 2026-08-07
**Scope:** The first production engineering workflow: `drawing_health_report()`, delivered by
`Civil3D.Tools.Health` (health DTOs, the pure analysis engine, the workflow orchestration over the
existing domain services, and the MCP tool). This is the reference implementation for every future
engineering workflow and the first consumer of the Phase 7A workflow framework.

---

## 1. Architecture

The workflow orchestrates the existing read-only Domain Services through the Workflow Framework.
Tools stay thin, analysis is pure, and Autodesk access stays confined to the domain layer:

```text
MCP Server
  → Named Pipe
  → Bridge (ToolDispatcher)
  → DrawingHealthReportTool (Civil3D.Tools.Health.Tools)
  → WorkflowDispatcher (validation, permission, timeout/cancellation, progress, events)
  → DrawingHealthWorkflowHandler
  → Workflow steps
      Validate Input            (analyzer options sanity check)
      Collect Drawing Information (ICivil3DSession + IDrawingStatisticsService)
      Collect Domain Data        (I*Service.GetAll() × 7 disciplines)
      Analyze Results            (HealthAnalyzer — pure, Autodesk-free)
      Generate Report            (compose DrawingHealthReport)
  → Domain Services (existing, read-only)
  → Protocol response
```

Layering rules (unchanged from Phase 4A):

- **Tools** orchestrate only — no Autodesk access, no analysis logic.
- **Workflow steps** resolve domain services from the workflow context; they never touch Autodesk APIs.
- **`HealthAnalyzer`** is a pure static engine with no state — trivially unit-testable.
- **Autodesk access** lives in the domain repositories/data sources exactly as before.

`Civil3D.Tools.Health` targets `net8.0-windows` because it references the discipline domain
projects (which host the Autodesk repositories). It contains no Autodesk references of its own.

---

## 2. Data Collection

Everything is read once, materialized into DTOs, then analyzed in memory:

| Stage | Services used | Data captured |
| --- | --- | --- |
| Collect Drawing Information | `ICivil3DSession`, `IDrawingStatisticsService` | drawing snapshot, layer/block/xref/entity/viewport/text-style/dimension-style/linetype/regapp/dictionary counts, approximate file size |
| Collect Domain Data | `IAlignmentService`, `ISurfaceService`, `IProfileService`, `ICorridorService`, `IPipeService`, `ICogoService`, `IStyleService` | all `GetAll()` collections |

Cancellation is honoured between every service read. The statistics service is invoked exactly
once per run (asserted by test). No geometry analysis is performed.

---

## 3. Analysis Rules

`HealthAnalyzer.Analyze(HealthData, HealthAnalyzerOptions)` runs every rule below. Findings are
ordered by severity (highest first), then by code.

| Code | Severity | Category | Trigger |
| --- | --- | --- | --- |
| `EMPTY_ALIGNMENTS` / `EMPTY_SURFACES` / `EMPTY_PROFILES` / `EMPTY_CORRIDORS` / `EMPTY_PIPE_NETWORKS` / `EMPTY_COGO_POINTS` / `EMPTY_STYLES` | Information | per discipline | collection is empty |
| `DUPLICATE_*_NAME` (per discipline) | Warning | per discipline | two objects share a name (case-insensitive) |
| `MISSING_*_DESCRIPTION` (per discipline) | Information | per discipline | object has no description |
| `ORPHANED_PROFILE` | Error | Profiles | profile references a non-existent alignment id |
| `ORPHANED_CORRIDOR` | Error | Corridors | corridor references a non-existent alignment id |
| `MISSING_STYLE` | Error | Alignments / Corridors | object references a non-existent style id |
| `MISSING_CODE_SET_STYLE` | Error | Corridors | corridor references a non-existent code set style id |
| `UNUSED_STYLE` | Information | Styles | alignment/corridor style id is not referenced by any inspected object |
| `LARGE_DRAWING` | Warning | Drawing | total entity count ≥ threshold (default 100 000) |
| `LARGE_MODEL_SPACE` | Warning | Drawing | model-space entity count ≥ threshold (default 50 000) |
| `LARGE_SURFACE` | Warning | Surfaces | surface point count ≥ threshold (default 500 000) |
| `LARGE_COGO_POINT_COLLECTION` | Warning | COGO Points | point count ≥ threshold (default 10 000) |
| `LOCKED_COGO_POINTS` | Warning | COGO Points | one or more points are locked |
| `READ_ONLY_DRAWING` | Warning | Drawing | the drawing file is read-only |
| `UNSAVED_CHANGES` | Information | Drawing | the drawing contains unsaved changes |

Thresholds are configurable through `HealthAnalyzerOptions` (defaults applied when omitted) so
tests and future tool parameters can tune them without touching the rules.

Only what the domain DTOs expose is inspected — no Autodesk API members are invented. Frozen- or
locked-layer checks, for example, would require layer information the current DTOs do not carry
and are therefore not emitted.

---

## 4. Report DTOs

All report DTOs live in `Civil3D.Tools.Health.Dtos`; they are immutable records containing only
serializable types (no Autodesk references, no dictionaries).

| DTO | Purpose |
| --- | --- |
| `DrawingHealthReport` | the tool result: drawing identity, `DrawingStatistics`, `HealthStatistics`, `Categories`, `Issues`, `Recommendations`, `WorkflowExecutionSummary` |
| `HealthCategory` | per-category severity roll-up (Name, TotalIssues, per-severity counts) |
| `HealthIssue` | one finding: Code, Severity, Category, Description, Reason, SuggestedAction, RelatedObject, optional secondary Recommendations |
| `HealthSeverity` | enum: Information, Warning, Error, Critical |
| `HealthStatistics` | severity roll-up over all findings plus the inspected `ObjectCount` |
| `HealthRecommendation` | a top-level recommendation: Description, Reason, SuggestedAction, RelatedObject |
| `WorkflowExecutionSummary` | WorkflowName, StartedAtUtc, FinishedAtUtc, Elapsed, TotalSteps, CompletedSteps |

Every finding carries the guidance triad — **Reason** (why it matters), **SuggestedAction** (what
to do) and **RelatedObject** (which object, when applicable) — plus a stable machine-readable
**Code** that callers can match against. Top-level **recommendations** summarise the drawing
state: resolve critical findings first, then errors, then review the remainder; a drawing with no
findings gets a single “The drawing is healthy.” recommendation.

---

## 5. Workflow Execution

`DrawingHealthWorkflow` (five steps, read-only, `RequiredPermission = ReadOnly`) is created fresh
per invocation with its own `DrawingHealthWorkflowState`; steps write materialized DTOs into the
state and the report step composes `DrawingHealthReport`. The handler
(`DrawingHealthWorkflowHandler`) runs the steps through `WorkflowHandlerBase` and returns the
report. The tool (`DrawingHealthReportTool`) binds `drawing_health_report()` with empty
parameters, dispatches through `IWorkflowDispatcher`, and maps failures through
`WorkflowErrorMapper` (E_NO_ACTIVE_DOCUMENT, E_INVALID_PARAMETERS, E_TIMEOUT, E_CANCELLED, …).

Progress is reported at every stage (Validate Input → Collect Drawing Information → Collect
Domain Data → Analyze Results → Generate Report → Complete) through the SDK `$/progress`
notification; cancellation and the 30-minute default timeout are handled by the dispatcher.

### Adding a new health check

1. **Add a rule** in `HealthAnalyzer` (a new private `Analyze…` method using the `Issue` helper,
   or extend an existing one). Use an existing stable code or add a new `UPPER_SNAKE` code.
2. **Extend `HealthData`** if the rule needs new data, and collect it in the matching workflow
   step (via an existing domain service — never directly through Autodesk).
3. **Add a test** in `HealthAnalyzerTests` for the rule, and extend the workflow test assertions
   if the new data changes the sample report.
4. **Document** the rule in the table in section 3.

### Adding a new workflow

Follow the same pattern: a new `Civil3D.Tools.*` project (or folder) with DTOs, a pure
analysis/service layer, a workflow + steps + handler, and a `WorkflowToolBase` tool. Register the
handler in `BridgeServiceCollectionExtensions`; the tool is discovered automatically by the SDK
assembly scanner. See `docs/WORKFLOW-FRAMEWORK.md` for the framework itself.

---

## 6. Performance & Cancellation

- One statistics read; one pass per domain collection; analysis is in-memory over materialized DTOs.
- No geometry analysis, no repeated Autodesk queries, no object loading beyond the DTOs.
- Cancellation is checked before every service read and inside the handler; the dispatcher
  enforces the timeout and links client cancellation.

## 7. Testing

`tests/Civil3D.Tools.Health.Tests` (27 tests):

- **Analysis rules** — every finding code, severity, category and related object; ordering;
  statistics and category roll-ups; recommendations; DTO serialization round-trip.
- **Workflow orchestration** — report composition from the canned sample, event publishing
  (WorkflowStarted/WorkflowCompleted), progress milestones through all five stages, pre-cancelled
  dispatch, no-active-drawing failure, statistics read exactly once.
- **Tool + SDK integration** — discovery, manifest generation, dispatch through the real SDK
  `ToolDispatcher` to a success envelope, `E_NO_ACTIVE_DOCUMENT` envelope, unknown-tool envelope.
