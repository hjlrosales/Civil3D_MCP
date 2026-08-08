# Autodesk MCP Platform — Design Validation Framework (Phase 7D)

**Status:** Implemented
**Date:** 2026-08-07
**Scope:** A reusable design validation framework plus one production workflow:
`design_validation_report()`, delivered by `Civil3D.Tools.Validation` (the rule framework, eight
initial rules, the rule engine, the workflow orchestration over the existing domain services and
the MCP tool). This is the reference implementation for every future validation workflow.

---

## 1. Validation Architecture

Validation rules are **independently registered, composable units**. The engine discovers them
through the container (`IEnumerable<IValidationRule>`), runs each one over a single materialized
snapshot and aggregates the findings into a consolidated report:

```text
MCP Server
  → Named Pipe
  → Bridge (ToolDispatcher)
  → DesignValidationReportTool (Civil3D.Tools.Validation.Tools)
  → WorkflowDispatcher (validation, permission, timeout/cancellation, progress, events)
  → DesignValidationWorkflowHandler
  → Workflow steps
      Validate Input             (at least one rule registered)
      Collect Domain Data         (ICivil3DSession + IDrawingStatisticsService + I*Service.GetAll() × 7)
      Execute Validation Rules    (ValidationEngine — times each rule, isolates failures)
      Aggregate Results           (summary, categories, recommendations)
      Generate Report             (compose DesignValidationReport)
  → ValidationEngine (Civil3D.Tools.Validation.Framework)
  → IValidationRule × 8 (Civil3D.Tools.Validation.Rules)
  → Domain Services (existing, read-only)
  → Protocol response
```

The dispatcher's 100% completion milestone is the sixth spec stage, "Complete".

Layering rules (unchanged from Phase 4A/7B):

- **Tools** orchestrate only — no Autodesk access, no rule logic.
- **Workflow steps** resolve domain services and the engine from the workflow context.
- **Rules** are pure consumers of `ValidationData`; they never touch Autodesk APIs and hold no state.
- **The engine** owns execution semantics: registration, ordering, timing, failure isolation, cancellation.
- **Autodesk access** lives in the domain repositories/data sources exactly as before.

`Civil3D.Tools.Validation` targets `net8.0-windows` because it references the discipline domain
projects (which host the Autodesk repositories). It contains no Autodesk references of its own.

---

## 2. Framework

| Type | Responsibility |
| --- | --- |
| `IValidationRule` | one rule: `Name`, `Category`, `Evaluate(ValidationData, IValidationContext) → IReadOnlyList<ValidationIssue>` |
| `IValidationContext` | correlation/session ids, logger, cancellation token |
| `ValidationContext` | default immutable implementation |
| `ValidationData` | the materialized snapshot (drawing, statistics, seven domain collections, object count) |
| `IValidationEngine` / `ValidationEngine` | runs the registered rules, times each, isolates failures, aggregates the result |
| `IValidationResult` / `ValidationEngineResult` | issues, categories, summary, recommendations |

**Engine semantics:**

- Rules are constructor-injected (`IEnumerable<IValidationRule>`), so new rules compose without
  engine changes — register a new `IValidationRule` in the composition root and it is discovered.
- Cancellation is honoured before every rule; a cancelled run throws `OperationCanceledException`.
- A rule that throws (other than cancellation) is logged, counted in `Summary.RuleFailures` and
  skipped — it never aborts the run or leaks an exception to the caller.
- Each rule is timed and logged with correlation/session ids.
- Findings are ordered severity-descending then code-ascending.

---

## 3. Initial Validation Rules

All rules use only data already exposed by the existing Domain Services. The spec's "objects on
locked/frozen layers" checks are **not implemented** because no per-layer state exists in the
current DTOs (only `LayerCount`); per the spec's "do not invent Autodesk API members" rule, they
will be added when the domain exposes layer data. `CogoPointInfo.IsLocked` is per-point, not
per-layer, and is not used to imply layer state.

| Rule | Code prefix | Severity | Data used |
| --- | --- | --- | --- |
| `duplicate-names` | `DUPLICATE_*_NAME` | Warning | names of alignments, surfaces, profiles, corridors, pipe networks |
| `missing-descriptions` | `MISSING_*_DESCRIPTION` | Information | description fields across six disciplines |
| `empty-collections` | `EMPTY_*` | Information | collection counts (only when the drawing has content) |
| `unresolved-references` | `UNRESOLVED_*` | Error | profile/corridor → alignment ids; alignment/corridor → style ids; corridor → code-set id |
| `unused-styles` | `UNUSED_STYLE` | Information | alignment/corridor style usage |
| `duplicate-cogo-point-numbers` | `DUPLICATE_COGO_POINT_NUMBER` | Warning | `CogoPointInfo.PointNumber` |
| `profiles-without-alignment` | `PROFILE_WITHOUT_ALIGNMENT` | Warning | `ProfileInfo.AlignmentId == 0` (no owner) |
| `pipe-networks-without-structures` | `PIPE_NETWORK_WITHOUT_STRUCTURES` | Warning | `PipeNetworkInfo.StructureCount == 0` |

---

## 4. Report DTOs

All report DTOs live in `Civil3D.Tools.Validation.Dtos`; they are immutable records containing only
serializable types (no Autodesk references, no dictionaries).

| DTO | Purpose |
| --- | --- |
| `DesignValidationReport` | the tool result: drawing identity, `Statistics`, `Summary`, `Categories`, `Issues`, `Recommendations`, `Execution` |
| `ValidationIssue` | one finding: `Code`, `Rule`, `Severity`, `Category`, `Title`, `Description`, `SuggestedAction`, `RelatedObject` |
| `ValidationSeverity` | enum: Information, Warning, Error, Critical |
| `ValidationCategory` | per-category severity roll-up (Name, TotalIssues, per-severity counts) |
| `ValidationSummary` | severity roll-up plus rule accounting: `RulesRegistered`, `RulesExecuted`, `RuleFailures`, `ObjectCount` |
| `ValidationRecommendation` | a top-level recommendation: `Title`, `Description`, `Severity`, `SuggestedAction`, `RelatedObject` |
| `ValidationExecutionSummary` | `WorkflowName`, start/finish timestamps, `Elapsed`, `TotalSteps`, `CompletedSteps` |

---

## 5. Rule Lifecycle & Registration

A validation rule has one lifecycle: **implement → register → executed → aggregated**. Rules are
stateless and must be safe to run in any order and any number of times.

### Implementing a rule

```csharp
public sealed class MyRule : IValidationRule
{
    public string Name => "my-rule";                       // stable machine-readable
    public string Category => "My Discipline";             // grouping for the report

    public IReadOnlyList<ValidationIssue> Evaluate(ValidationData data, IValidationContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        // ... inspect data.Alignments / data.Surfaces / ... and return findings
    }
}
```

### Registering a rule

In the Bridge composition root (`BridgeServiceCollectionExtensions`), register the rule; the
engine picks it up automatically:

```csharp
services.AddSingleton<IValidationRule, MyRule>();
```

No engine, workflow, tool or protocol changes are required to add a rule.

---

## 6. Execution & Observability

- **Progress** is published by every workflow step; the six spec stages appear as milestones
  (Validate Input, Collect Domain Data, Execute Validation Rules, Aggregate Results, Generate
  Report, and the dispatcher's Complete milestone at 100%).
- **Events**: the dispatcher publishes `WorkflowStarted` / `WorkflowCompleted` / `WorkflowFailed`
  through the existing `IDomainEventDispatcher`.
- **Cancellation** is honoured between steps and between rules; a cancelled run throws
  `OperationCanceledException`.
- **Errors**: the Validate Input step rejects a container with no registered rules
  (`WorkflowErrorCode.InvalidParameters`); a missing active drawing surfaces as
  `InvalidParameters` at the workflow layer and maps to `E_NO_ACTIVE_DOCUMENT` at the tool layer.
  A rule that throws is isolated by the engine and counted in `Summary.RuleFailures` — raw
  exceptions never cross the protocol boundary.
- **Logging**: per-rule execution time and finding count, per-step object counts, aggregated
  severity counts, correlation ID and session ID.

---

## 7. Extending Validation with New Rules

1. **Rule** — add a class in `Civil3D.Tools.Validation.Rules` implementing `IValidationRule`;
   keep it pure (read `ValidationData` only, no Autodesk access, no shared state).
2. **Register** — add one line in the Bridge composition root.
3. **Tests** — add an isolated rule test in `ValidationRuleTests` plus one engine assertion in
   `ValidationEngineTests`; the workflow and SDK paths are already covered by
   `DesignValidationWorkflowTests` and `DesignValidationReportToolTests`.
4. **Document** — add a row to the rule table in this document.

The framework itself (engine, workflow, tool, Bridge wiring) needs no changes.

---

## 8. Relationship to Other Frameworks

`design_validation_report` is a **read-only workflow**: it uses the Workflow Framework and the
Query/Domain services but never the Command Framework. The rule engine is a new, dedicated
framework: the Query Framework is for data access, the Command Framework is for edits, and the
Validation Framework is for composable, data-driven quality checks. See also
`docs/WORKFLOW-FRAMEWORK.md`, `docs/DRAWING-HEALTH-REPORT.md` and `docs/PROJECT-SUMMARY-REPORT.md`.
