# Autodesk MCP Platform — Surface Comparison Workflow (Phase 7F)

**Status:** Implemented
**Date:** 2026-08-07
**Scope:** One production engineering workflow, `surface_comparison_report()`, delivered by
`Civil3D.Tools.Surface` (the comparison DTOs, the pure comparison engine, the workflow
orchestration over the surface domain service and the MCP tool). Read-only end to end.

---

## 1. Workflow Architecture

The surface comparison runs through the established workflow framework: the tool
(`SurfaceComparisonReportTool`) inherits from `WorkflowToolBase`, takes a typed
`SurfaceComparisonRequest`, the workflow (`SurfaceComparisonWorkflow`) declares five stages, and
the pure comparison engine (`SurfaceComparer`) turns the two loaded surface snapshots into the
report:

```text
MCP Server
  → Named Pipe
  → Bridge (ToolDispatcher)
  → SurfaceComparisonReportTool (Civil3D.Tools.Surface.Tools)
  → WorkflowDispatcher (Civil3D.Domain.Workflows)
  → SurfaceComparisonWorkflow steps (Civil3D.Tools.Surface.Workflow)
      Validate Input → Load Surface Metadata → Load Comparison Data
      → Analyze Differences → Generate Report → (Complete milestone)
  → ISurfaceService.GetById (Civil3D.Domain.Surfaces.Services)
  → SurfaceComparer (Civil3D.Tools.Surface.Analysis, pure, Autodesk-free)
  → SurfaceComparisonReport (Civil3D.Tools.Surface.Dtos)
  → Protocol Response
```

The tool, workflow, engine and report mirror the Phase 7E quantity-takeoff layout exactly: the
same dispatcher, the same `WorkflowToolBase`, the same progress/cancellation/event semantics and
mapping of `DomainException(EntityNotFound)` to `E_OBJECT_NOT_FOUND`.

---

## 2. Request DTO

`SurfaceComparisonRequest` (camelCase on the wire):

| Property | Type | Notes |
|---|---|---|
| `existingSurfaceId` | `long` | Id of the existing (reference) surface. Must be > 0. |
| `proposedSurfaceId` | `long` | Id of the proposed (candidate) surface. Must be > 0 and differ. |
| `includeStatistics` | `bool` | Default `true`; the numeric statistics section. |
| `includeRecommendations` | `bool` | Default `true`; the recommendation section. |

Validation failures (missing ids, identical ids) map to `E_INVALID_PARAMETERS`; a missing
surface maps to `E_OBJECT_NOT_FOUND`; no active drawing maps to `E_NO_ACTIVE_DOCUMENT`.

---

## 3. Comparison Methodology

`SurfaceComparer.Compare(SurfaceComparisonData)` is a pure, static, Autodesk-free engine. It
compares **only metrics the domain layer exposes** on `SurfaceInfo`: name, kind (surface type),
point count, minimum elevation, maximum elevation and mean elevation — six metrics. Triangle
counts, boundary counts, extents and build status are **not** exposed by the current DTOs and
are omitted rather than invented.

### Per-metric comparisons

Every metric produces a `SurfaceMetricComparison` with the existing and proposed values rendered
as strings (so numeric and textual metrics share one shape), a unit and an `isSignificant` flag
(name equality, kind equality, point-count equality, elevation deltas beyond 0.0001).

### Differences

A `SurfaceDifference` is produced only when a metric actually differs:

| Metric | Severity rule |
|---|---|
| name | `Information` |
| kind | `Warning` |
| pointCount | `Warning` when the delta ≥ `PointCountDifferenceRatio` (default 25%) of the larger count, else `Information` |
| min/max elevation | `Warning` when the delta ≥ `ElevationRangeTolerance` (default 2.0 units), else `Information` |
| mean elevation | `Warning` when the delta ≥ `MeanElevationTolerance` (default 1.0 units), else `Information` |

Differences are ordered by severity, then metric key. The summary verdict is `Review Required`
when any difference reaches `Warning` or higher, otherwise `Compatible`.

### Statistics (optional)

`SurfaceComparisonStatistics` reports deltas as proposed − existing: point-count delta and
percent of the larger count, min/max/mean elevation deltas and the elevation-range (max − min)
delta. Produced only when `includeStatistics` is true.

### Recommendations (optional)

Recommendations are derived purely from the compared metrics:

| Title | Trigger |
|---|---|
| Surface appears outdated | Proposed point count < `OutdatedSurfaceRatio` (default 50%) of the existing count |
| Large point-count difference | Point-count delta ≥ `PointCountDifferenceRatio` |
| Large elevation range difference | Range delta ≥ `ElevationRangeTolerance` |
| Review before volume calculations | Mean-elevation delta ≥ `MeanElevationTolerance` |
| Surfaces are compatible | No significant differences and no other recommendation |

Thresholds live in `SurfaceComparisonOptions` (defaults above) so future callers can tune them
without touching the engine.

---

## 4. Report DTOs

| DTO | Contents |
|---|---|
| `SurfaceComparisonReport` | Summary + metrics + differences + optional statistics + optional recommendations + execution summary |
| `SurfaceComparisonSummary` | Both surface ids/names, metric/difference/recommendation counts, verdict |
| `SurfaceMetricComparison` | One compared metric (key, name, existing/proposed values, unit, significant) |
| `SurfaceDifference` | One differing metric (key, name, description, severity) |
| `SurfaceComparisonStatistics` | Numeric deltas |
| `ComparisonRecommendation` | Title, description, severity, suggested action, related surface |
| `ComparisonSeverity` | `Information` < `Warning` < `Error` < `Critical` |
| `WorkflowExecutionSummary` | Workflow name, timestamps, elapsed, step accounting |

All DTOs are immutable records/serializable enums with no Autodesk types.

---

## 5. Performance & Robustness

- Each surface is loaded exactly once through `ISurfaceService.GetById` (read-only).
- The comparison runs entirely in memory on the two DTO snapshots; no repeated repository calls.
- Cancellation is checked before each step and between the two loads.
- Progress milestones are published for every stage plus the dispatcher's `Complete` milestone.
- Errors map through the standard `WorkflowErrorMapper`; raw Autodesk exceptions never cross the
  protocol boundary.

## 6. Limitations

- Only the six metrics above; no triangle/boundary counts, extents or build status (unavailable
  through the current domain DTOs).
- No cut/fill or volume calculations — deliberately out of scope for this phase.
- `includeStatistics`/`includeRecommendations` are request-level toggles; per-metric selection is
  not supported.

## 7. Extending Future Surface Workflows

- Add a metric: extend `SurfaceInfo` in the domain layer (with repository support), then add the
  comparison row to `SurfaceComparer.BuildMetrics`, the difference rule to
  `BuildDifferences` and any recommendation to `BuildRecommendations`.
- Tune thresholds: pass a custom `SurfaceComparisonOptions` via `SurfaceComparisonData.Options`.
- Reuse the pattern: a future cut/fill or volume workflow would collect the same surfaces
  through `ISurfaceService` and add its own pure calculation engine, exactly like this phase
  added `SurfaceComparer` next to `QuantityCalculator`.
