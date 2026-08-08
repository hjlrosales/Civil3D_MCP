# Autodesk MCP Platform — Corridor Analysis Workflow (Phase 7H)

**Status:** Implemented
**Date:** 2026-08-07
**Scope:** One production engineering workflow, `corridor_analysis_report()`, delivered by
`Civil3D.Tools.Corridor` (the corridor-analysis DTOs, the pure analysis engine, the workflow
orchestration over the corridor domain service and the MCP tool). Read-only end to end.

---

## 1. Workflow Architecture

The corridor analysis follows the established 7E/7F/7G pattern. The tool
(`CorridorAnalysisReportTool`) inherits from `WorkflowToolBase`, takes a typed
`CorridorAnalysisRequest`, the workflow (`CorridorAnalysisWorkflow`) declares five stages, and
the pure analysis engine (`CorridorAnalyzer`) turns the loaded `CorridorInfo` snapshots into
the report:

```text
MCP Server
  → Named Pipe
  → Bridge (ToolDispatcher)
  → CorridorAnalysisReportTool (WorkflowToolBase)
  → CorridorAnalysisWorkflow (5 stages + Complete milestone)
  → ICorridorService            (domain layer; never Autodesk objects here)
  → CorridorAnalyzer            (pure, Autodesk-free analysis engine)
  → CorridorAnalysisReport      (immutable DTO)
  → Protocol Response
```

The tool resolves the handler through `IWorkflowDispatcher`; the Bridge registers
`CorridorAnalysisWorkflowHandler` in DI. Workflow steps resolve their dependencies from the
workflow context — the corridor service and logger — and never touch Autodesk APIs.

## 2. Request

`CorridorAnalysisRequest` (all properties optional except the toggles' defaults):

| Property | Type | Meaning |
| --- | --- | --- |
| `corridorId` | `long?` | The corridor to analyze; `null` analyzes every corridor in the drawing. |
| `includeStatistics` | `bool` | Default `true`; include the aggregate statistics section. |
| `includeRecommendations` | `bool` | Default `true`; include the recommendation section. |

## 3. Workflow Stages

| # | Stage | Work |
| --- | --- | --- |
| 1 | Validate Input | Rejects non-positive corridor ids (`E_INVALID_PARAMETERS`). |
| 2 | Load Corridor Data | Resolves one corridor by id, or every corridor via `GetAll()`. Missing id → `DomainException(EntityNotFound)` → `E_OBJECT_NOT_FOUND`. |
| 3 | Analyze Corridors | Runs `CorridorAnalyzer.Analyze` (summaries, verdict, issues, statistics). |
| 4 | Generate Recommendations | Runs `CorridorAnalyzer.BuildRecommendations` when enabled. |
| 5 | Generate Report | Composes the final `CorridorAnalysisReport` with the execution summary. |
| — | Complete | Dispatcher completion milestone (100%). |

Progress is reported after every stage, cancellation is honoured between reads, and every
stage logs with correlation/session ids.

## 4. Available Metrics (and what is omitted)

`CorridorInfo` (the only corridor snapshot the domain exposes) contains:

| Metric | Used for |
| --- | --- |
| `Id`, `Name`, `Description` | Identity, missing-description issue |
| `StyleId`, `CodeSetStyleId` | Missing-style issues, health status |
| `AlignmentId` | Primary baseline alignment reference (surfaced in the summary) |
| `BaselineCount` | No-baselines issue, complexity, statistics |
| `CorridorSurfaceCount` | No-surfaces issue, complexity, takeoff suitability, statistics |

**Omitted rather than invented** (not exposed by current DTOs): region count, assembly usage,
target mappings, rebuild status, corridor length, surface-generation status and frequency
settings. These do not appear anywhere in the report.

## 5. Health Status and Issues

Each corridor gets a short status: `Healthy`, `No Baselines`, `No Surfaces`, or
`Needs Review`. Issues carry a stable machine-readable `code`, title, description, severity
and the owning corridor id/name:

| Code | Severity | Trigger |
| --- | --- | --- |
| `noBaselines` | Error | `BaselineCount == 0` |
| `noSurfaces` | Warning | `CorridorSurfaceCount == 0` |
| `missingStyle` | Warning | `StyleId == null` |
| `missingCodeSetStyle` | Information | `CodeSetStyleId == null` |
| `missingDescription` | Information | Empty description |

Overall verdict: `No Corridors` (empty set), `Attention Required` (any Error issue),
`Review Recommended` (any Warning issue), `Healthy` otherwise.

## 6. Recommendations

Generated only from available metrics, ordered by severity:

| Title | Severity | Trigger |
| --- | --- | --- |
| Review generated surfaces | Warning | No corridor surfaces |
| Review style assignments | Warning | Missing corridor/code set style |
| Large corridor complexity | Information | ≥ 4 baselines or ≥ 3 corridor surfaces (thresholds in `CorridorOptions`) |
| Suitable for quantity takeoff | Information | Has baselines and corridor surfaces |
| No corridors in the drawing | Information | Empty corridor set |

## 7. Statistics

`CorridorStatistics` (null when disabled): corridor count, total baselines, total corridor
surfaces, corridors with/without baselines, corridors with/without surfaces, and average
baselines per corridor. All derived from the same in-memory snapshots — no re-querying.

## 8. Performance

- Loads the corridor set **once** (single `GetAll()` or `GetById()` call).
- Analysis and recommendations run entirely **in memory** over immutable DTOs.
- No repeated repository calls; no geometry; no Autodesk objects cross the domain boundary.

## 9. Errors

| Scenario | Result |
| --- | --- |
| No active document | `E_NO_ACTIVE_DOCUMENT` |
| Corridor id does not exist | `E_OBJECT_NOT_FOUND` |
| Non-positive corridor id | `E_INVALID_PARAMETERS` |
| Cancellation | `OperationCanceledException` → cancellation envelope |
| Unexpected failure | Mapped to protocol errors; Autodesk exceptions never cross the boundary |

## 10. Extension Points

- **New corridor metrics** (regions, assemblies, targets, rebuild status, length): extend
  `CorridorInfo` + the repository data source; the analyzer and this document update together.
- **New health checks**: add codes to `BuildIssues` / `BuildSummary`; keep verdict and
  statistics derived from the same single pass.
- **New corridor workflows** (e.g., rebuild, target mapping): reuse the same
  `WorkflowToolBase` + `IWorkflowStep` pattern against `ICorridorService` or future services.

## 11. Testing

28 tests cover: analyzer verdicts/issues/statistics per corridor shape, recommendation
triggers and omissions, report serialization round-trip, workflow orchestration (all-versus
single corridor, progress milestones, cancellation, events, validation, missing corridor),
and SDK dispatch (typed binding, all error envelopes, discovery, manifest).
