# Autodesk MCP Platform — Quantity Takeoff Workflow (Phase 7E)

**Status:** Implemented
**Date:** 2026-08-07
**Scope:** One production engineering workflow, `quantity_takeoff_report()`, delivered by
`Civil3D.Tools.Quantity` (the quantity DTOs, the pure calculation engine, the workflow
orchestration over the existing domain services and the MCP tool). Read-only end to end.

---

## 1. Workflow Architecture

The quantity takeoff runs through the established workflow framework: the tool
(`QuantityTakeoffReportTool`) inherits from `WorkflowToolBase`, the workflow
(`QuantityTakeoffWorkflow`) declares six stages, and the pure calculation engine
(`QuantityCalculator`) turns a materialized snapshot into the report:

```text
MCP Server
  → Named Pipe
  → Bridge (ToolDispatcher)
  → QuantityTakeoffReportTool (Civil3D.Tools.Quantity.Tools)
  → WorkflowDispatcher (Civil3D.Domain.Workflows)
      Validate Input
      Collect Drawing Information  (ICivil3DSession, IDrawingStatisticsService)
      Collect Domain Data          (IAlignmentService, IProfileService, ISurfaceService,
                                    ICorridorService, IPipeService, ICogoService, IStyleService)
      Calculate Quantities         (QuantityCalculator — pure, in memory)
      Aggregate Results            (per-category roll-ups already produced by the engine)
      Generate Report              (compose QuantityTakeoffReport + execution summary)
      Complete                     (dispatcher milestone, 100%)
  → Protocol Response
```

No tool or workflow step touches the Autodesk API. Data is collected **once** through the
existing read-only domain services; every calculation runs in memory over the materialized
DTOs. Progress is published after every stage; cancellation is honoured between reads.

---

## 2. Calculation Methodology

The `QuantityCalculator` is a stateless, pure, Autodesk-free engine. It performs a single pass
over the materialized `QuantityData` snapshot and produces:

* **Quantity items** — one line per measurement, keyed for machine consumption:

  | Category | Items |
  | --- | --- |
  | Alignments | `alignment.count`, `alignment.total_length` (Length) |
  | Profiles | `profile.count`, `profile.total_length` (Length) |
  | Surfaces | `surface.count`, `surface.total_points` |
  | Corridors | `corridor.count`, `corridor.total_baselines`, `corridor.total_surfaces` |
  | Pipes | `pipe_network.count`, `pipe.count`, `structure.count` |
  | COGO Points | `cogo_point.count`, `cogo_point.locked_count` |
  | Styles | `style.count` plus one line per non-empty `StyleKind` |
  | Drawing | layer/block/xref/entity/model-space/paper-space/viewport/text-style/dimension-style/linetype counts and `drawing.approximate_size_bytes` (Bytes) |

* **Per-category summaries** — each `QuantitySummary` carries the number of items in the
  category and the sum of its *count-unit* quantities. Measured lengths and file sizes are
  excluded from the sum so the aggregate stays dimensionally meaningful.

* **Aggregate statistics** — `QuantityStatistics`: total domain objects, total linear length
  (alignments + profiles), total surface points, corridor baselines/surfaces, pipes,
  structures, locked COGO points, entity volume and approximate drawing size.

**Availability rule:** only metrics already exposed by the domain DTOs are produced. If a
metric is not available (for example cut/fill volumes), it is omitted rather than invented.

**Grouping and filtering:** the report deliberately takes no input parameters. It is a
read-everything-once inventory, so "configurable grouping" is expressed structurally — the
per-category `Summaries` are the grouping — and `QueryRequest`-style filtering/pagination does
not apply to an aggregate report (every object is counted, none is skipped). If a future
phase needs a filtered takeoff, the calculator can accept a `QueryRequest`-derived filter
without changing the workflow or the protocol.

---

## 3. Report DTOs

All report DTOs live in `Civil3D.Tools.Quantity.Dtos`; they are immutable records containing
only serializable types (no Autodesk references, no dictionaries).

| DTO | Purpose |
| --- | --- |
| `QuantityTakeoffReport` | the tool result: `Overview`, `Items`, `Summaries`, `Statistics`, `Execution` |
| `QuantityOverview` | drawing identity (name, path, version, Civil 3D version, fingerprint) |
| `QuantityItem` | one line: `Category`, `Key`, `Label`, `Quantity`, `Unit`, `Detail` |
| `QuantityCategory` | enum: Alignments, Profiles, Surfaces, Corridors, Pipes, CogoPoints, Styles, Drawing |
| `QuantityUnit` | enum: Count, Length, Bytes |
| `QuantitySummary` | per-category roll-up: `ItemCount`, `TotalQuantity`, `TotalLabel` |
| `QuantityStatistics` | aggregate totals across all disciplines |
| `WorkflowExecutionSummary` | workflow timing and step accounting |

---

## 4. Adding Metrics

To add a new quantity line (for example a future area/volume metric):

1. Add the unit to `QuantityUnit` if it does not exist (e.g. `Area`).
2. Add a builder branch in `QuantityCalculator.BuildItems` reading only from the existing
   domain DTOs — never from the Autodesk API directly.
3. Optionally surface the total in `QuantityStatistics`.
4. Add a calculator test asserting the line and its unit, and extend the workflow/tool test
   if the sample data covers it.

Because the engine is pure and stateless, extending the report never touches the workflow,
the tool or the protocol.

---

## 5. Performance & Logging

The workflow opens exactly the existing read-only service calls, materializes the DTOs once
and calculates in memory. Logging records the workflow name, step, collected object count,
item count and correlation/session ids; every step also reports progress.

---

## 6. Testing

The test project (`tests/Civil3D.Tools.Quantity.Tests`) covers: calculator behaviour
(per-discipline items, summaries, aggregate statistics, empty data, null statistics),
serialization round-trip, workflow orchestration (progress stages, events, cancellation,
no-active-drawing failure), and end-to-end SDK dispatch (discovery, manifest, protocol
response envelopes).
