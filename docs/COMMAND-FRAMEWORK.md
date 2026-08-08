# Autodesk MCP Platform — Command & Transaction Framework (Phase 5A)

**Status:** Implemented
**Date:** 2026-08-07
**Scope:** The reusable command execution infrastructure (`Civil3D.Domain.Commands`), the
write-transaction pipeline, validators, confirmation/permission checks, domain events, the tool
base (`Civil3D.Tools.Commands`) and the Autodesk-backed write transaction provider in the Bridge.
No production editing commands exist yet — those arrive in Phase 5B and must use this framework.

---

## 1. Architecture

Every editing operation flows through the fixed command pipeline:

```text
MCP Tool (CommandToolBase)  →  Command  →  CommandDispatcher
                                              │
                    validation → permission → confirmation → progress
                                              │
                                      TransactionPipeline
                                              │
                            begin → lock → handler → commit | rollback
                                              │
                              domain events (Started/Committed/Completed | Failed/RolledBack)
                                              │
                                       protocol response
```

**Rules:**

- A command handler never opens a transaction. The pipeline begins one (document-locked), hands
  it to the handler, and commits it when the handler returns; any failure rolls back.
- Tools never access Autodesk transactions directly.
- Handlers do the work through repositories/services; repositories do the Autodesk access.

---

## 2. Projects

| Project | Contents | Autodesk? |
|---|---|---|
| `Civil3D.Domain.Commands` | Commands, handlers, validators, dispatcher, transaction pipeline, undo context, events, progress. | No (provider seam) |
| `Civil3D.Tools.Commands` | `CommandToolBase`, `IConfirmationGate`, error mapping. | No (reference only) |
| Bridge `Data/AutodeskTransactionProvider.cs` | Real `ITransactionProvider`: active document, `Document.LockDocument()`, `TransactionManager.StartTransaction()`. | Yes |

---

## 3. Core Contracts

| Interface | Responsibility |
|---|---|
| `ICommand` / `ICommand<TResult>` | Declares `Name`, `RequiredPermission`, `IsReadOnly`, `RequiresConfirmation` (+ `Confirmation` metadata). |
| `ICommandHandler<TCommand, TResult>` | Executes the command; receives the active `IWriteTransaction` (null for read-only) and the effective token. |
| `ICommandValidator<TCommand>` | Validates a command; any number can be registered, all run, failures aggregate. |
| `ICommandDispatcher` | The pipeline orchestrator (validation → permission → confirmation → transaction → events → logging). |
| `ICommandExecutionContext` | Correlation/session ids, token, progress, undo, granted permission, confirmation state. |
| `ITransactionPipeline` | Owns begin/commit/rollback/dispose, nested detection, read-only detection, timeout, cancellation. |
| `ITransactionProvider` / `IWriteTransaction` | Seam for the host transaction (Autodesk in the Bridge, fake in tests). |
| `IUndoContext` / `IUndoUnit` | Abstraction for the future AutoCAD undo integration (currently no-op). |
| `IDomainEventDispatcher` | Publishes `CommandStarted`/`CommandCompleted`/`CommandFailed`/`TransactionCommitted`/`TransactionRolledBack`. |
| `IProgressReporter` | Progress stages from the pipeline (adapted to the SDK reporter by the tool base). |

---

## 4. The Pipeline Steps

1. **Validation** — every registered `ICommandValidator<TCommand>` runs; failures aggregate into
   `CommandException(ValidationFailed)` → `E_VALIDATION_FAILED`. Nothing else runs on failure.
2. **Permission** — `command.RequiredPermission > context.EffectivePermission` →
   `E_PERMISSION_DENIED`. The granted level comes from the tool's own `[McpTool]` permission.
3. **Confirmation** — commands with `RequiresConfirmation == true` run only when
   `context.ConfirmationGranted`; otherwise `E_CONFIRMATION_REQUIRED`. The bridge-side
   `IConfirmationGate` records the client's granted answer; until that channel is wired the null
   gate denies everything (safe default).
4. **Transaction** — writing commands run in one document-locked write transaction; read-only
   commands run with no transaction.
5. **Commit/Rollback** — success commits and publishes `TransactionCommitted`; any failure
   (handler exception, commit failure, timeout, cancellation) rolls back, publishes
   `TransactionRolledBack` with the reason and rethrows the original failure.
6. **Events & logging** — `CommandStarted`/`CommandCompleted`/`CommandFailed` plus logs of
   command name, execution time, validation result, transaction duration, rollback reason,
   correlation and session ids.

---

## 5. Transaction Pipeline Semantics

- **Begin** — one transaction per writing command, created by `ITransactionProvider` (the
  Autodesk provider also takes the document lock). The provider seam keeps the pipeline fully
  unit-testable.
- **Nested detection** — a second `Execute` while one is active throws
  `TransactionAlreadyActive` → `E_TRANSACTION_FAILED` (Autodesk is single-threaded; nesting is
  a bug, not a feature).
- **Read-only detection** — `TransactionOptions.ReadOnly` (from `ICommand.IsReadOnly`) skips
  begin/commit/rollback entirely; the work receives a null transaction.
- **Timeout** — `TransactionOptions.Timeout` drives a linked `CancellationTokenSource`; the work
  receives that token (via the pipeline's two-argument delegate), so a slow handler observes the
  timeout and the pipeline rolls back with reason `timeout` → `E_TIMEOUT`.
- **Cancellation** — the caller's token rolls back with reason `cancelled` → `E_CANCELLED`.
- **Automatic disposal** — the transaction is always disposed in `finally`, and the fake and
  Autodesk implementations both guard their state machine (Active → Committed | RolledBack →
  Disposed) against illegal transitions.
- **Rollback reason** — recorded in `TransactionRolledBack` and the pipeline log (timeout,
  cancelled, or the failure type name).

---

## 6. Error Mapping

`CommandErrorMapper` (in `Civil3D.Tools.Commands`) maps every framework failure to a stable
protocol code; raw exceptions never cross the pipe:

| Failure | Protocol code |
|---|---|
| `ValidationFailed` | `E_VALIDATION_FAILED` |
| `PermissionDenied` | `E_PERMISSION_DENIED` |
| `ConfirmationRequired` | `E_CONFIRMATION_REQUIRED` |
| `NoActiveDocument` | `E_NO_ACTIVE_DOCUMENT` |
| `TransactionFailed`, `TransactionAlreadyActive` | `E_TRANSACTION_FAILED` |
| `TransactionTimeout` | `E_TIMEOUT` |
| `Cancelled` | `E_CANCELLED` |
| `ObjectNotFound` | `E_OBJECT_NOT_FOUND` |
| `InvalidParameters` | `E_INVALID_PARAMETERS` |
| anything else (incl. `DomainException`) | `E_INTERNAL` / domain codes map to their read-only equivalents |

`DomainException` thrown by handlers/repositories passes through the dispatcher unchanged so its
stable code maps to the matching protocol error.

---

## 7. How to Build a New Editing Command (Phase 5B recipe)

1. **Command** — `sealed class XCommand : ICommand<XResult>` declaring `Name`,
   `RequiredPermission`, `IsReadOnly = false`, `RequiresConfirmation` and the input properties.
2. **Handler** — `sealed class XCommandHandler : ICommandHandler<XCommand, XResult>` doing the
   work through discipline repositories (which use `transaction.Handle` for Autodesk access).
   Throw `DomainException`/`CommandException` on failure.
3. **Validators** — one or more `ICommandValidator<XCommand>` (name required, ids exist, …).
4. **Tool** — `sealed class XTool : CommandToolBase<TIn, TOut, XCommand, XResult>` with
   `[McpTool(..., Permission = ToolPermission.ModifyDrawing, Risk = ...)]`;
   `CreateCommand(input, context)` maps the tool input to the command;
   `MapResult(result)` maps the command result to the output DTO.
5. **Register** — in the Bridge DI: `services.AddTransient<ICommandHandler<XCommand, XResult>, XCommandHandler>();`
   and `services.AddTransient<ICommandValidator<XCommand>, XValidator>();`. The tool is discovered
   automatically by the catalog (no registration).

---

## 8. DI Registration (Bridge)

```csharp
services.AddSingleton<ITransactionProvider, AutodeskTransactionProvider>();
services.AddSingleton<ITransactionPipeline, TransactionPipeline>();
services.AddSingleton<IDomainEventDispatcher, InMemoryDomainEventDispatcher>();
services.AddSingleton<IUndoContext>(_ => NullUndoContext.Instance);
services.AddSingleton<IConfirmationGate>(_ => NullConfirmationGate.Instance);
services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
```

Tests register the same shape with an in-memory `ITransactionProvider`.

---

## 9. Undo & Confirmation Status

- **Undo** — `IUndoContext`/`IUndoUnit` exist as contracts with a no-op implementation. The
  future real implementation opens an AutoCAD `UndoRecord` around the command's transaction;
  handlers already interact only with the abstraction, so nothing downstream changes.
- **Confirmation** — the protocol's `ConfirmationRequest`/`ConfirmationResponse` contracts and
  `ClientCapabilities.SupportsConfirmation` already exist; the server elicits, the bridge records
  the answer in `IConfirmationGate`. The null gate denies everything until that channel is wired,
  so dangerous commands cannot run unconfirmed by accident.
- **Reentrancy warning (Phase 5B)** — `CommandToolBase` calls `IConfirmationGate.IsGranted` before
  dispatching, i.e. before the document lock is taken, so a blocking gate round-trip cannot
  deadlock a locked document. Still, the bridge is single-threaded: the future server-side
  implementation of the gate must elicit confirmation out-of-band (surface a
  `ConfirmationRequest` and record the `ConfirmationResponse` as it arrives) rather than awaiting
  a nested request on the main loop.

---

## 10. Tests

- `Civil3D.Domain.Commands.Tests` — validation aggregation, permission, confirmation, progress
  stages, event ordering, commit, rollback (handler failure + commit failure), nested detection,
  read-only detection, timeout, cancellation, automatic disposal.
- `Civil3D.Tools.Commands.Tests` — `CommandToolBase` error mapping (all codes) and end-to-end
  execution through the real SDK dispatcher: tool discovery → manifest → routing → command tool →
  command pipeline → write transaction → commit/rollback → protocol response envelope, with
  mocked Autodesk services.
