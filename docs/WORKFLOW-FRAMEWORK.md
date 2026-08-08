# Autodesk MCP Platform — Workflow Framework (Phase 7A)

**Status:** Implemented
**Date:** 2026-08-07
**Scope:** The reusable execution infrastructure for long-running engineering workflows:
`Civil3D.Domain.Workflows` (interfaces, dispatcher pipeline, progress, steps, error codes) and
`Civil3D.Tools.Workflows` (`WorkflowToolBase` + protocol error mapping). This document is the
standard reference for every future workflow (Phase 7B and beyond). No engineering workflows are
implemented yet.

---

## 1. Architecture

A workflow coordinates multiple domain services and repositories in clearly defined stages with
progress reporting, cancellation, timeouts and structured logging. Tools never orchestrate the
stages themselves — they build a workflow definition and dispatch it:

```text
MCP Tool (WorkflowToolBase)            Civil3D.Tools.Workflows
  → CreateWorkflow(input) → IWorkflow<TResult>
WorkflowDispatcher                       Civil3D.Domain.Workflows
  → validation (IWorkflowValidator<TWorkflow>)
  → permission check (IWorkflow.RequiredPermission)
  → timeout-linked cancellation envelope
    → IWorkflowHandler<TWorkflow, TResult> (constructor-injected domain services)
      → WorkflowStepExecutor: runs IWorkflowStep in order (10–90% milestones)
      → ProduceResultAsync → TResult
  → WorkflowResult<TResult> + WorkflowCompleted/WorkflowFailed events + logging
  → protocol response (WorkflowErrorMapper → ErrorCode)
```

## 2. Core Types

| Type | Responsibility |
| --- | --- |
| `IWorkflow` / `IWorkflow<TResult>` | Name, required permission, optional timeout, ordered steps. Data-and-stages definition created by the tool. |
| `IWorkflowStep` | One reusable stage; receives the context and effective token, returns `WorkflowStepOutcome` (continue or stop early). |
| `IWorkflowHandler<TWorkflow, TResult>` | Executes the workflow; prefer `WorkflowHandlerBase` which runs the steps then calls `ProduceResultAsync`. Resolved by DI; constructor injection for domain services. |
| `IWorkflowValidator<TWorkflow>` | Structural validation before any step runs; reuses `ValidationResult`/`ValidationFailure` from the Command framework. |
| `IWorkflowDispatcher` | The pipeline. Resolves handlers/validators by closed generic type — workflows register freely, no switches. |
| `IWorkflowContext` | Correlation/session ids, cancellation token, progress, logger, container (for steps), configuration, granted permission, start time. Autodesk-free. |
| `IWorkflowProgress` / `WorkflowProgress` | Tracks percent, current step/message, elapsed and estimated remaining; forwards reports through the domain `IProgressReporter` seam. |
| `WorkflowResult<TResult>` | Typed outcome with timing; also serialized into the protocol response payload. |
| `WorkflowException` / `WorkflowErrorCode` | Single failure type carrying a stable code; the tool layer maps codes to protocol errors. |

## 3. Workflow Lifecycle

1. **Build** — the tool binds input parameters into a workflow definition (steps + payload).
2. **Validate** — all registered validators run; any failure stops execution (`E_VALIDATION_FAILED`).
3. **Check permission** — `RequiredPermission` is compared with the tool's manifest-granted level (`E_PERMISSION_DENIED`).
4. **Execute** — the handler runs the steps (progress 10–90%), then produces the result.
5. **Complete** — the dispatcher reports 100%, publishes `WorkflowCompleted`, logs execution time.
6. **Fail** — any failure publishes `WorkflowFailed` and maps to a protocol error; `DomainException` passes through with its stable code.

## 4. Execution Pipeline

The `WorkflowDispatcher` is deliberately parallel to the `CommandDispatcher`:

- validation failures are aggregated across all validators;
- the timeout envelope is a linked `CancellationTokenSource` (`workflow.Timeout`, falling back to
30 minutes when null or non-positive); the dispatcher distinguishes caller cancellation
(`E_CANCELLED`) from timeout (`E_TIMEOUT`) by checking the original token;
- steps check cancellation between each other and receive the effective token;
- a `DomainException` from a step/handler is never re-wrapped — its code maps directly to the
protocol error (`E_OBJECT_NOT_FOUND`, `E_NO_ACTIVE_DOCUMENT`, `E_TRANSACTION_FAILED`, …);
- any other step failure becomes `StepFailed` → `E_INTERNAL`;
- correlation and session ids flow into events and every log line.

## 5. Progress Reporting

The tool base wraps the SDK `IProgressReporter` (wired to `$/progress`) in a `WorkflowProgress`.
Milestones reported by the framework: `Validated` (5%), `Checked` (10%), per-step stages (10–90%),
`Steps complete` (90%), `Complete` (100%). Steps can refine progress via
`context.Progress.Report(percent, step, message)`; elapsed and estimated remaining are always
available from the tracker.

## 6. Cancellation and Timeout

- **Caller cancellation / `$/cancel`** → `OperationCanceledException` inside the pipeline →
`WorkflowException(Cancelled)` → `E_CANCELLED`.
- **Workflow timeout** (`IWorkflow.Timeout`, or the 30-minute default) → `WorkflowException(Timeout)`
→ `E_TIMEOUT`.
- Steps must observe the token (check it and pass it to awaited work) to be cancellable.

## 7. Adding a New Workflow

1. **DTOs** — immutable Autodesk-free records for the workflow payload and its result.
2. **Workflow** — implement `IWorkflow<TResult>` (name, permission, optional timeout, ordered steps).
3. **Steps** — implement `IWorkflowStep` per stage; resolve domain services from `context.Services`
or share the handler's constructor-injected services.
4. **Handler** — derive from `WorkflowHandlerBase<TWorkflow, TResult>` and implement `ProduceResultAsync`;
constructor-inject the domain services (e.g. `ISurfaceService`).
5. **Validators** — implement `IWorkflowValidator<TWorkflow>` for structural rules.
6. **Tool** — derive from `WorkflowToolBase<TIn, TOut, TWorkflow, TResult>`, implement
`CreateWorkflow` and `MapResult`, decorate with `[McpTool(...)]`. All orchestration stays in the base.
7. **Register** — in `BridgeServiceCollectionExtensions`: handler + validators by closed generic type
(reuse the shared `WorkflowDispatcher` — no per-workflow pipeline code).

Example registration:

```csharp
services.AddTransient<IWorkflowHandler<SampleWorkflow, SampleResult>, SampleWorkflowHandler>();
services.AddTransient<IWorkflowValidator<SampleWorkflow>, SampleWorkflowValidator>();
```

## 8. Relationship with the Other Frameworks

| Framework | Purpose | Interaction |
| --- | --- | --- |
| **Query** | Read-only list/get/search with filtering, sorting, paging | Workflow steps use `I*Service` query methods to gather inputs |
| **Commands** | Single, validated, transactional writes | A workflow can invoke commands through `ICommandDispatcher` inside a step for stage-level writes |
| **Workflows** | Long-running multi-stage engineering operations | Composes services/repositories; never touches Autodesk APIs itself |

All three share the same conventions: constructor injection, immutable DTOs, domain/repository
layering, `DomainException` pass-through, and protocol error mapping in the tool layer.

## 9. Coding Standards

- Workflows/steps/handlers contain **no Autodesk references**.
- No static mutable state; no reflection; no `dynamic`; no anonymous DTOs.
- Steps are side-effect-free of orchestration — progress, cancellation and error mapping are the
framework's job.
- Log workflow name, step, execution time, cancellation/timeout and correlation/session ids.
- Never let raw exceptions cross the pipe.
