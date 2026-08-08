# Autodesk MCP Platform — LandXML Export Workflow (Phase 7I)

**Status:** Implemented
**Date:** 2026-08-07
**Scope:** One production export workflow, `export_landxml()`, delivered by
`Civil3D.Tools.Export` (the LandXML export DTOs, the `ILandXmlExporter` abstraction, the pure
analysis engine, the output validator, the workflow orchestration over the exporter and domain
services, and the MCP tool). Creates an external file only; the drawing is never modified.

---

## 1. Workflow Architecture

The export workflow follows the 7G pattern of isolating an engine behind an abstraction. The
tool (`ExportLandXmlTool`) inherits from `WorkflowToolBase`, takes a typed
`LandXmlExportRequest`, and the workflow (`LandXmlExportWorkflow`) declares six stages:

```text
MCP Server
  → Named Pipe
  → Bridge (ToolDispatcher)
  → ExportLandXmlTool (WorkflowToolBase, ToolPermission.Export)
  → LandXmlExportWorkflow (6 stages + Complete milestone, CommandPermission.Export)
  → ICorridorService / ISurfaceService / …  (read-only counts; never Autodesk objects)
  → ILandXmlExporter                      (the only export-capable boundary)
  → LandXmlOutputValidator                (exists, size, well-formed XML)
  → LandXmlExportReport                   (immutable DTO)
  → Protocol Response
```

The workflow depends only on `ILandXmlExporter` — never on Autodesk export APIs. The Bridge
registers `Civil3DLandXmlExporter` and `LandXmlExportWorkflowHandler` in DI. This is the
platform's **first Export-permission tool** (`ToolPermission.Export` /
`CommandPermission.Export`); the dispatcher enforces the permission level.

## 2. Request

`LandXmlExportRequest`:

| Property | Default | Meaning |
| --- | --- | --- |
| `outputPath` | — (required) | Full output path; must end in `.xml`. |
| `includeAlignments` | `true` | Include alignments. |
| `includeProfiles` | `true` | Include profiles. |
| `includeSurfaces` | `true` | Include surfaces. |
| `includeCorridors` | `false` | Include corridors (support-dependent). |
| `includePipeNetworks` | `false` | Include pipe networks (support-dependent). |
| `overwriteExisting` | `false` | Allow replacing an existing file at the output path. |

## 3. Workflow Stages

| # | Stage | Work |
| --- | --- | --- |
| 1 | Validate Input | Output path present + `.xml` suffix, at least one object type enabled, no existing file unless overwrite is allowed → `E_INVALID_PARAMETERS`. |
| 2 | Collect Export Data | Counts each enabled type exactly once through the read-only domain services. |
| 3 | Build Export Options | Composes the immutable `LandXmlExportData` snapshot handed to the exporter. |
| 4 | Execute Export | Runs `ILandXmlExporter.Export`, timed and logged. |
| 5 | Validate Output | For a completed export: file exists, non-empty, well-formed XML; failure → `E_INTERNAL` (StepFailed). Skipped for not-supported results. |
| 6 | Generate Report | Composes the final report with the execution summary. |
| — | Complete | Dispatcher completion milestone (100%). |

Progress is reported after every stage, cancellation is honoured between reads, and every
stage logs with correlation/session ids.

## 4. The Exporter Abstraction

- `ILandXmlExporter.Export(LandXmlExportData)` → `LandXmlExportResult` — the single boundary
  the workflow sees.
- `LandXmlExportData`: the validated options plus the collected per-type counts.
- `LandXmlExportResult`: `LandXmlExportStatus` (`Exported` | `NotSupported`), a structured
  reason, the output path, file size, the `ExportedObject`/`SkippedObject` lists and the
  completion timestamp.

### Production implementation and honest limitation

The production `Civil3DLandXmlExporter` (in the Autodesk-free tools project, like the 7G
calculator) reports a structured `NotSupported` result: reliable LandXML export through the
Civil 3D managed API requires a live interactive document context that the read-only workflow
layer does not perform, so no file is written and no export capability is invented. A future
Autodesk-backed exporter assembly (referencing the conditional AeccDbMgd assemblies like the
domain data sources) can swap in behind the same interface; the workflow, DTOs and tests do
not change.

## 5. Output Validation

`LandXmlOutputValidator.Validate(path)` checks, after a completed export: the file exists, is
non-empty, and parses as well-formed XML (a full `XDocument` load). Full LandXML **schema**
validation is deliberately out of scope for this phase. An `Exported` result whose file fails
validation fails the workflow (`E_INTERNAL`), because claiming success for a missing or broken
file would be dishonest.

## 6. Report

`LandXmlExportReport` combines:

- `ExportSummary` — status (`Exported`/`Not Supported`), output path, file size, exported and
  skipped counts, the not-supported reason.
- `ExportStatistics` — per-type collected counts (only enabled types), total considered,
  exported/skipped totals.
- `ExportedObject` / `SkippedObject` — what was written and what was skipped (with reasons).
- `ExportRecommendation` — derived only from the actual outcome:
  - *Export completed successfully* (Information),
  - *Review skipped objects* (Warning) when anything was skipped,
  - *Export not supported by installed API* (Warning),
  - *No objects to export* (Information) when nothing enabled exists.
- `WorkflowExecutionSummary` — timing and step accounting.

## 7. Errors

| Scenario | Result |
| --- | --- |
| No active document | `E_NO_ACTIVE_DOCUMENT` |
| Empty path, non-`.xml` path, no object types enabled, existing file without overwrite | `E_INVALID_PARAMETERS` |
| Exported but file missing/malformed | `E_INTERNAL` |
| Insufficient permission | `E_PERMISSION_DENIED` |
| Cancellation | cancellation envelope |

## 8. Performance

- Object counts collected **once** (one `Count()` per enabled type).
- A single export operation through the abstraction; analysis and report composition entirely
  in memory.
- No repeated repository calls; no geometry; no Autodesk objects cross the domain boundary.

## 9. Limitations and Future Work

- **Import is out of scope** (explicitly deferred).
- **Schema validation is out of scope**; only well-formedness is checked.
- **Real LandXML writing** requires an Autodesk-backed exporter (see §4); the production
  implementation currently reports a structured not-supported result, consistent with the 7G
  calculator precedent and the platform's "never invent Autodesk behavior" rule.
- Corridors and pipe networks default to **off** because their export support depends on the
  installed API; enabling them is a per-request decision.

## 10. Testing

29 tests cover: analyzer summaries/statistics/recommendations (including disabled-type
counting), output validation with real temp files (well-formed, malformed, missing),
serialization round-trip, workflow orchestration (real file written and validated,
not-supported path, invalid output → StepFailed, input validation incl. overwrite semantics,
permission enforcement, progress, cancellation, events, exporter substitution), and SDK
dispatch (typed binding, all error envelopes, discovery, manifest with Export permission).
