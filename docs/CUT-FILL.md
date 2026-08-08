# Autodesk MCP Platform — Cut & Fill Analysis Workflow (Phase 7G)

**Status:** Implemented
**Date:** 2026-08-07
**Scope:** One production engineering workflow, `calculate_cut_fill()`, delivered by
`Civil3D.Tools.CutFill` (the cut/fill DTOs, the `ICutFillCalculator` abstraction, the pure
analysis engine, the workflow orchestration over the surface domain service and the MCP tool).
Read-only end to end.

---

## 1. Workflow Architecture

The cut/fill analysis is the platform's first geometry-aware engineering workflow, and the
first to introduce an engine abstraction. The tool (`CalculateCutFillTool`) inherits from
`WorkflowToolBase`, takes a typed `CutFillRequest`, the workflow (`CutFillWorkflow`) declares
six stages, and the pure analysis engine (`CutFillAnalyzer`) turns the raw calculator output
into the report:

```text
MCP Server
  → Named Pipe
  → Bridge (ToolDispatcher)
  → CalculateCutFillTool (Civil3D.Tools.CutFill.Tools)
  → WorkflowDispatcher (Civil3D.Domain.Workflows)
  → CutFillWorkflow steps (Civil3D.Tools.CutFill.Workflow)
      Validate Input → Load Surfaces → Prepare Calculation
      → Execute Calculation → Analyze Results → Generate Report → (Complete milestone)
  → ISurfaceService.GetById (Civil3D.Domain.Surfaces.Services)
  → ICutFillCalculator (Civil3D.Tools.CutFill.Abstractions)  ← abstraction
  → Civil3DCutFillCalculator (production) | test doubles | future engines
  → CutFillAnalyzer (Civil3D.Tools.CutFill.Analysis, pure, Autodesk-free)
  → CutFillReport (Civil3D.Tools.CutFill.Dtos)
  → Protocol Response
```

The tool, workflow, analyzer and report mirror the Phase 7F surface-comparison layout exactly:
the same dispatcher, the same `WorkflowToolBase`, the same progress/cancellation/event
semantics and the same mapping of `DomainException(EntityNotFound)` to `E_OBJECT_NOT_FOUND`.

---

## 2. Request DTO

`CutFillRequest` (camelCase on the wire):

| Property | Type | Notes |
|---|---|---|
| `existingSurfaceId` | `long` | Id of the existing ground (reference) surface. Must be > 0. |
| `proposedSurfaceId` | `long` | Id of the proposed (design) surface. Must be > 0 and differ. |
| `includeStatistics` | `bool` | Default `true`; the derived statistics section. |
| `includeRecommendations` | `bool` | Default `true`; the recommendation section. |

Validation failures (missing ids, identical ids) map to `E_INVALID_PARAMETERS`; a missing
surface maps to `E_OBJECT_NOT_FOUND`; no active drawing maps to `E_NO_ACTIVE_DOCUMENT`.

---

## 3. The Calculator Abstraction

The workflow never touches Civil 3D APIs. It depends only on

```csharp
public interface ICutFillCalculator
{
    CutFillCalculationResult Calculate(CutFillCalculationData data);
}
```

- `CutFillCalculationData` carries the two loaded `SurfaceInfo` snapshots and the analysis
  thresholds.
- `CutFillCalculationResult` carries the status (`Computed`/`NotSupported`), an optional
  `NotSupportedReason`, the cut/fill/net volumes (net signed: positive = net cut) and the
  surface area used.
- The contract requires implementations to return a structured `NotSupported` result instead of
  throwing or inventing API behavior when no reliable volume path exists.

This makes the workflow independently testable — test doubles implement the interface — and
lets a future real engine replace the production implementation without touching the workflow
or the tools.

---

## 4. The Production Calculator and the Platform Limitation

`Civil3DCutFillCalculator` is the production implementation for the Civil 3D host. The Civil 3D
managed API exposes volume data only through volume surfaces (`TinVolumeSurface`/
`GridVolumeSurface`), which must be created inside a document write transaction and added to
the drawing database before their statistics become readable. That is a drawing modification,
which the read-only workflow must not perform, and the current domain layer exposes no read-only
volume path. Per the platform availability rule (the same rule used in Phases 7E/7F — omit
rather than invent), this limitation is isolated inside the calculator: it returns a structured
`NotSupported` result with the reason, and the report carries it as `summary.status =
"notSupported"` with `summary.notSupportedReason`.

This is a deliberate, documented behaviour, not a gap: the interface exists precisely so that a
future phase can implement real volume computation (for example creating a transient volume
surface inside an editing command pipeline) behind the same contract.

---

## 5. Calculation & Analysis Methodology

### Volumes (calculator output)

Cut volume, fill volume, signed net volume (cut − fill) and the surface area used. All in
drawing units.

### Verdict (analyzer, from calculated values only)

| Condition | Verdict |
|---|---|
| Volumes not supported | `Not Supported` |
| Cut + fill = 0 | `No Earthwork` |
| \|net\| ÷ total ≤ `BalanceThreshold` (10%) | `Balanced Earthwork` |
| net ≥ 0 | `Predominantly Cut` |
| otherwise | `Predominantly Fill` |

### Statistics (optional)

Cut/fill/net as percentages of the total volume, and the cut ÷ fill ratio. Omitted when volumes
are not supported, total is zero, or statistics are disabled.

### Recommendations (optional, derived from calculated values only)

| Title | Trigger |
|---|---|
| No earthwork required | Total volume = 0 |
| Balanced earthwork | \|net\| ÷ total ≤ `BalanceThreshold` |
| Predominantly cut | Not balanced and net ≥ 0 |
| Predominantly fill | Not balanced and net < 0 |
| Significant net export | Net > 0 and \|net\| ÷ total ≥ `SignificantImbalanceRatio` (25%) |
| Significant net import | Net < 0 and \|net\| ÷ total ≥ `SignificantImbalanceRatio` |
| Verify surface quality before construction | Point-count delta ≥ `SurfaceQualityPointRatio` (25%) of the larger surface |

Thresholds live in `CutFillOptions` (defaults above) so future callers can tune them without
touching the engine.

Note that the classification and haulage recommendations can co-occur: a strongly imbalanced
result produces both `Predominantly cut` (the verdict) and `Significant net export` (the
haulage concern) — they answer different questions and both are derived from the calculated
volumes.

### Differences (context, always present)

Four per-metric surface differences (point count, min/max/mean elevation) contextualise the
volumes and remain available even when volumes are not supported.

---

## 6. Report DTOs

| DTO | Contents |
|---|---|
| `CutFillReport` | Summary + differences + optional statistics + optional recommendations + execution summary |
| `VolumeSummary` | Surface ids/names, status, reason, cut/fill/net volumes, area, verdict, balanced flag |
| `VolumeStatistics` | Cut/fill/net percentages and cut ÷ fill ratio |
| `VolumeDifference` | One contextual surface difference (key, name, existing/proposed values, description) |
| `CutFillRecommendation` | Title, description, severity, suggested action |
| `CutFillSeverity` | `Information` < `Warning` < `Error` < `Critical` |
| `WorkflowExecutionSummary` | Workflow name, timestamps, elapsed, step accounting |

All DTOs are immutable records/serializable enums with no Autodesk types.

---

## 7. Performance & Robustness

- Each surface is loaded exactly once through `ISurfaceService.GetById` (read-only).
- The calculation runs once through `ICutFillCalculator`; analysis is entirely in memory.
- The calculation step is timed with a `Stopwatch` and the duration is logged.
- Cancellation is checked before each step and between the two loads.
- Progress milestones are published for every stage plus the dispatcher's `Complete` milestone.
- Errors map through the standard `WorkflowErrorMapper`; raw Autodesk exceptions never cross the
  protocol boundary.

## 8. Limitations

- No real volume computation yet: the production calculator returns a structured `NotSupported`
  result (see section 4). The report still returns successfully with the reason, and the
  workflow/tool/analyzer are fully exercised by tests through test doubles.
- No cut/fill grids, cross-sections, or per-station quantities — deliberately out of scope.

## 9. Future Extension Points

- **Real volume engine:** implement `ICutFillCalculator` behind a write-transaction path (e.g.
  a `TinVolumeSurface` created within the Phase 5A command pipeline), swap the DI registration
  from `Civil3DCutFillCalculator` to the new engine — the workflow and tool do not change.
- **More metrics:** extend `CutFillOptions` with new thresholds and add rules to
  `CutFillAnalyzer.BuildRecommendations`.
- **Per-region quantities:** the calculator contract can grow a region parameter on
  `CutFillCalculationData` without changing the pipeline shape.
- **Reuse the pattern:** a future alignment- or corridor-based earthwork workflow would collect
  its inputs through domain services and add its own pure engine, exactly like this phase added
  `CutFillAnalyzer` next to `SurfaceComparer` and `QuantityCalculator`.
