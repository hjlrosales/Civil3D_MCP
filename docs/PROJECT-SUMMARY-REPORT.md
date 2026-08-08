# Autodesk MCP Platform — Project Summary Report (Phase 7C)

**Status:** Implemented
**Date:** 2026-08-07
**Scope:** The second production engineering workflow: `project_summary_report()`, delivered by
`Civil3D.Tools.Project` (project DTOs, the pure analysis engine, the workflow orchestration over the
existing domain services, and the MCP tool). The report gives AI clients immediate context about
the active Civil 3D drawing and is the reference for future project-level report workflows.

---

## 1. Architecture

The workflow orchestrates the existing read-only Domain Services through the Workflow Framework.
Tools stay thin, analysis is pure, and Autodesk access stays confined to the domain layer:

```text
MCP Server
  → Named Pipe
  → Bridge (ToolDispatcher)
  → ProjectSummaryReportTool (Civil3D.Tools.Project.Tools)
  → WorkflowDispatcher (validation, permission, timeout/cancellation, progress, events)
  → ProjectSummaryWorkflowHandler
  → Workflow steps
      Validate Input             (analyzer options sanity check)
      Collect Drawing Information (ICivil3DSession + IDrawingStatisticsService)
      Collect Domain Objects      (I*Service.GetAll() × 7 disciplines)
      Analyze Relationships       (ProjectAnalyzer — pure, Autodesk-free)
      Generate Summary            (compose ProjectSummaryReport)
  → Domain Services (existing, read-only)
  → Protocol response
```

Layering rules (unchanged from Phase 4A/7B):

- **Tools** orchestrate only — no Autodesk access, no analysis logic.
- **Workflow steps** resolve domain services from the workflow context; they never touch Autodesk APIs.
- **`ProjectAnalyzer`** is a pure static engine with no state — trivially unit-testable.
- **Autodesk access** lives in the domain repositories/data sources exactly as before.

`Civil3D.Tools.Project` targets `net8.0-windows` because it references the discipline domain
projects (which host the Autodesk repositories). It contains no Autodesk references of its own.

---

## 2. Summary Generation

Everything is read once, materialized into DTOs, then analyzed in memory:

| Stage | Services used | Data captured |
| --- | --- | --- |
| Collect Drawing Information | `ICivil3DSession`, `IDrawingStatisticsService` | drawing snapshot, layer/block/xref/entity/viewport/text-style/dimension-style/linetype/regapp/dictionary counts, approximate file size |
| Collect Domain Objects | `IAlignmentService`, `ISurfaceService`, `IProfileService`, `ICorridorService`, `IPipeService`, `ICogoService`, `IStyleService` | all `GetAll()` collections |

Cancellation is honoured between every service read. The statistics service is invoked exactly
once per run (asserted by test). No geometry analysis is performed. Pipes and structures are
rolled up from the pipe networks (no separate repository traversal). The spec's optional data
sources that have no domain service yet — assemblies, pressure networks, reference objects and
feature lines — are intentionally omitted rather than invented ("Do not invent Autodesk API
members"); they will appear once their domain projects exist.

---

## 3. Complexity Scoring

The analyzer classifies the drawing into four bands (`Small`, `Medium`, `Large`, `Enterprise`)
using a weighted score over object volume, entity volume, references and heavy objects:

```text
score = entities / 5000
      + xrefs × 3
      + domainObjects / 20
      + corridors × 4
      + pipeNetworks × 3
      + surfaces × 2
```

Classification (defaults in `ProjectSummaryOptions`):

| Band | Condition |
| --- | --- |
| Small | `score < 10` |
| Medium | `10 ≤ score < 25` |
| Large | `25 ≤ score < 50` |
| Enterprise | `score ≥ 50` |

All thresholds are configurable via `ProjectSummaryOptions` (SmallScoreThreshold,
MediumScoreThreshold, LargeScoreThreshold, LargeDrawingEntityThreshold, MaxNameListLength).
Negative thresholds fail the workflow with `InvalidParameters` during the Validate Input step.
The classification is also reflected in `ComplexityAssessment.Score` and `Reason`.

---

## 4. Recommendations

Recommendations are ordered highest-priority first, then alphabetically by title. The `ProjectAnalyzer`
currently emits five:

| Title | Trigger | Priority |
| --- | --- | --- |
| Audit broken references | any reference failed to resolve (orphaned object or missing style) | High |
| Review unused styles | alignment/corridor styles not referenced by any inspected object | Low (Medium when > 3) |
| Large drawing optimization | total entity count ≥ `LargeDrawingEntityThreshold` (100,000) | Medium |
| Missing metadata | any alignment/surface/profile/corridor/network/COGO point without a description | Low |
| Reference synchronization | the drawing references one or more xrefs | Low |

Each recommendation carries `Title`, `Description`, `Priority` (`RecommendationPriority`:
Low/Medium/High/Critical) and `SuggestedAction`. Reference checks cover profiles → alignments,
corridors → alignments, and alignment/corridor → style references.

---

## 5. Report DTOs

All report DTOs live in `Civil3D.Tools.Project.Dtos`; they are immutable records containing only
serializable types (no Autodesk references, no dictionaries).

| DTO | Purpose |
| --- | --- |
| `ProjectSummaryReport` | the tool result: `Overview`, `Inventory`, `References`, `Complexity`, `Statistics`, `Recommendations`, `Execution` |
| `ProjectOverview` | drawing identity: name, path, DWG version, Civil 3D version, modified/read-only state, layout, fingerprint, open documents |
| `ObjectInventory` | per-discipline counts: alignments, profiles, surfaces, corridors, pipe networks, pipes, structures, COGO points, styles, layers, blocks, xrefs, entities (model/paper), viewports, text/dimension styles, linetypes |
| `ReferenceSummary` | reference integrity: xref count, references checked/resolved/missing, orphaned object and missing-style counts, healthy status |
| `ComplexityAssessment` | `Classification` (`ProjectComplexity`), `Score`, `Reason` |
| `ProjectStatistics` | top-level totals: domain objects, entities, xrefs, references checked/healthy/missing |
| `ProjectRecommendation` | one recommendation: `Title`, `Description`, `Priority` (`RecommendationPriority`), `SuggestedAction` |
| `WorkflowExecutionSummary` | `WorkflowName`, start/finish timestamps, `Elapsed`, `TotalSteps`, `CompletedSteps` |

Enums: `ProjectComplexity` (Small, Medium, Large, Enterprise) and `RecommendationPriority` (Low,
Medium, High, Critical). Name lists in the inventory are capped at `MaxNameListLength` (default
100) to keep the payload bounded on very large drawings.

---

## 6. Execution & Observability

- **Progress** is published by every step through the workflow context (percent + stage + message);
  the final report lands at 100%.
- **Events**: the dispatcher publishes `WorkflowStarted` / `WorkflowCompleted` / `WorkflowFailed`
  through the existing `IDomainEventDispatcher`.
- **Cancellation** is honoured at the start of every step and between every service read; a
  cancelled run throws `OperationCanceledException` (a token cancelled before dispatch surfaces
  the exception before the dispatcher wraps it — the framework's established behaviour).
- **Errors**: the `Validate Input` step rejects negative analyzer thresholds with
  `WorkflowErrorCode.InvalidParameters`; a missing active drawing surfaces as
  `InvalidParameters` at the workflow layer and is mapped to `E_NO_ACTIVE_DOCUMENT` at the tool
  layer. Autodesk exceptions never cross the protocol boundary.
- **Logging**: workflow name, step name, object counts, complexity classification, recommendation
  count, correlation ID and session ID are logged at information level per step.

---

## 7. Extending Future Project Reports

To add a new section or discipline to the report:

1. **DTO** — add a record in `Civil3D.Tools.Project.Dtos` (immutable, serializable, Autodesk-free)
   and reference it from `ProjectSummaryReport`.
2. **Collection** — add the discipline collection to `ProjectData` and populate it in the
   `CollectDomainObjectsStep` (or a new step) via the existing domain service `GetAll()`.
3. **Analysis** — add a private rule method in `ProjectAnalyzer` (pure, static, no state) and
   wire it into `Analyze`. Keep thresholds in `ProjectSummaryOptions`.
4. **Recommendations** — return new `ProjectRecommendation` entries from
   `BuildRecommendations`; ordering (priority desc, then title) is applied automatically.
5. **Tests** — add analyzer unit tests plus one orchestration assertion in
   `ProjectSummaryWorkflowTests`; the integration path is already covered by
   `ProjectSummaryReportToolTests`.

The workflow itself (five steps), the tool, the Bridge registration and the dispatcher need no
changes for a new report section.

---

## 8. Relationship to Other Frameworks

`project_summary_report` is a **read-only workflow**: it uses the Workflow Framework and the
Query/Domain services but never the Command Framework. Future editing operations must keep using
commands; future read-only reports should follow this workflow pattern (see also
`docs/DRAWING-HEALTH-REPORT.md` and `docs/WORKFLOW-FRAMEWORK.md`).
