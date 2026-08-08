# Autodesk MCP Platform — Editing Tools (Phase 5B/5C)

**Status:** Implemented
**Date:** 2026-08-09 (Phase 5C: `create_pipe` added)
**Scope:** The first production editing commands — `rename_alignment` and `rename_surface`
(Phase 5B), and the first *creation* command, `create_pipe` (Phase 5C) — exercising the complete
write pipeline end-to-end. This document is the standard reference for all future write
operations.

---

## 1. Architecture

Every editing operation flows through the fixed command pipeline; tools never touch Autodesk
transactions:

```text
MCP Tool (CommandToolBase)          Civil3D.Tools.Editing
  → RenameXxxRequest → RenameXxxCommand
CommandDispatcher (validation → permission → confirmation → progress)
  → TransactionPipeline (begin → document lock → handler → commit | rollback)
    → RenameCommandHandler<TCommand> (undo unit)
      → RenameXxxService (business rules + ObjectRenamed event)
        → XxxRenameRepository (Autodesk rename inside the active transaction)
  → protocol response (RenameResult)
```

**Layering rules (non-negotiable):**

- **Tools** contain orchestration only: bind input → command, map result → DTO.
- **Services** contain business rules: existence, no-op detection, uniqueness, event raising.
- **Repositories** contain Autodesk access only: open objects, set names, map outcomes.
- **Handlers** coordinate: open/commit the undo unit, delegate to the service, roll back on failure.
- No tool or handler opens a transaction. The pipeline owns begin/commit/rollback/dispose.

---

## 2. Command Lifecycle

1. `CommandToolBase.CreateCommand` builds the command (capturing the current name via the read
   service so no-op renames are reported clearly).
2. `CommandDispatcher` publishes `CommandStarted`, runs every registered
   `ICommandValidator<TCommand>` (structural name rules), checks permission
   (`ModifyDrawing`) and confirmation (policy-driven).
3. `TransactionPipeline` begins a document-locked write transaction and hands it to the handler
   together with the timeout/cancellation-linked token.
4. `RenameCommandHandler<TCommand>` opens an `IUndoContext` unit, calls the rename service, and
   commits the undo unit on success (rolls it back on failure).
5. The service rejects no-op renames (`InvalidName`) and duplicate names (`DuplicateName`),
   performs the Autodesk rename through the write repository, and raises `ObjectRenamed`.
6. The pipeline commits; the dispatcher publishes `CommandCompleted`/`TransactionCommitted` and
   logs name, ids, execution time, correlation and session. Any failure rolls back and maps to a
   stable protocol error.

---

## 3. Validation Flow

Structural validation runs in the validators **before any transaction**:

- Name is not empty / whitespace.
- Name length ≤ 64 characters.
- Name contains only supported characters (`\w ._()-'`).

Existence, no-op and uniqueness are enforced **inside the write transaction** by the service
(`EntityNotFound`, `InvalidName`, `DuplicateName` domain codes), so the checks are atomic with
the write. Validator failures → `E_VALIDATION_FAILED`; service domain failures map to
`E_OBJECT_NOT_FOUND` / `E_VALIDATION_FAILED`.

---

## 4. Transaction & Confirmation

- **Document lock + write transaction** — provided by `AutodeskTransactionProvider`; the
  handler receives `IWriteTransaction` and passes it to the service/repository.
- **Rollback** — any failure (service rule, Autodesk error, timeout, cancellation) aborts the
  transaction, publishes `TransactionRolledBack` with the reason, and rethrows the original
  `DomainException`/`CommandException`.
- **Confirmation** — driven by `BridgeOptions.RequireConfirmationForRename`. When enabled the
  command sets `RequiresConfirmation`; the pipeline denies execution unless
  `IConfirmationGate.IsGranted` returns true (the null gate denies everything until the
  confirmation channel is wired — a safe default).

---

## 5. Repository Responsibilities

- `IAlignmentRenameRepository.Rename(IWriteTransaction, long id, string newName)` casts the
  transaction handle to the Autodesk `Transaction`, opens the object for write, sets `Name`,
  and returns an immutable `RenameOutcome(ObjectId, PreviousName, CurrentName)`.
- Write repositories resolve the active database through `IAutodeskDocumentContext.ExecuteWrite`
  (a dedicated write seam added in Phase 5B — never `ExecuteRead`), so document availability
  and exception mapping are shared with the read path.
- `AutodeskAlignmentRenameRepository` / `AutodeskSurfaceRenameRepository` live in the domain
  discipline projects (where the Autodesk references live) and are registered in the bridge.
- Repositories throw `DomainException` (`EntityNotFound`, `TransactionFailed`) and never leak
  Autodesk exceptions.

## 6. Service Responsibilities

`RenameAlignmentService` / `RenameSurfaceService` (identical shape):

1. `_read.GetById(id)` — existence + current name (`EntityNotFound`).
2. No-op check — same name (case-insensitive) → `InvalidName`.
3. `_read.ExistsName(newName, exceptId: id)` — uniqueness (`DuplicateName`).
4. `_write.Rename(transaction, id, newName)` — the Autodesk rename.
5. Publish `ObjectRenamed` and return `RenameResult`.

Services depend on the read repository (uniqueness), the write repository (rename) and the
`IDomainEventDispatcher` — all constructor-injected, all Autodesk-free.

---

## 7. Events & Undo

- **Events** — `CommandStarted`, `CommandCompleted`, `CommandFailed`,
  `TransactionCommitted`, `TransactionRolledBack` come from the framework; the rename raises
  `ObjectRenamed(ObjectType, ObjectId, PreviousName, NewName, CorrelationId, SessionId)`.
  No subscribers exist yet.
- **Undo** — `RenameCommandHandler` opens an `IUndoContext` unit per rename and commits/rolls
  it back. The abstraction keeps handlers free of AutoCAD undo APIs; the real AutoCAD
  integration lands later without touching handlers. Note: the unit is committed when the
  handler succeeds, just before the pipeline commits the transaction; the future AutoCAD
  implementation must couple the undo record to the transaction commit (rolling the record back
  if the transaction fails to commit).

---

## 8. How to Build a Future Editing Tool

1. **DTOs** — immutable request record + result record in the tool project.
2. **Command** — extend `RenameCommandBase` (or a new base) with a stable `Name`, the payload
   properties and the discipline `ObjectType`; `RequiredPermission`/`IsReadOnly` are inherited.
3. **Validator** — `ICommandValidator<TCommand>` using `NameRules` for structural checks.
4. **Service** — discipline `RenameXxxService : IRenameXxxService` with the read repository
   (uniqueness), the write repository and the event dispatcher.
5. **Write repository** — `IXxxRenameRepository` interface + Autodesk implementation in the
   discipline domain project.
6. **Handler** — reuse `RenameCommandHandler<TCommand>` (or an equivalent thin handler with
   undo).
7. **Tool** — `CommandToolBase<TIn, TOut, TCommand, TResult>` with `[McpTool(...,
   Permission = ToolPermission.ModifyDrawing)]`; auto-discovered, no registration.
8. **DI** — register repository, service, handler and validator in
   `BridgeServiceCollectionExtensions`.
9. **Tests** — follow `Civil3D.Tools.Editing.Tests`: in-memory drawing + fake repositories,
   tool-level pipeline tests and dispatcher integration tests.

---

## 9. Error Mapping

| Failure | Domain code | Protocol code |
|---|---|---|
| Object missing | `EntityNotFound` | `E_OBJECT_NOT_FOUND` |
| Empty / too long / bad characters | validator | `E_VALIDATION_FAILED` |
| No-op rename | `InvalidName` | `E_VALIDATION_FAILED` |
| Duplicate name | `DuplicateName` | `E_VALIDATION_FAILED` |
| Confirmation denied | — | `E_CONFIRMATION_REQUIRED` |
| Permission | — | `E_PERMISSION_DENIED` |
| Write failure / commit failure | `TransactionFailed` | `E_TRANSACTION_FAILED` |
| Timeout / cancellation | — | `E_TIMEOUT` / `E_CANCELLED` |

Raw Autodesk exceptions never cross the protocol boundary — every failure is translated by
`CommandErrorMapper` and `ReadOnlyRepositoryBase`/the write repository wrappers.

---

## 10. `create_pipe` (Phase 5C)

The first *creation* command — as opposed to rename's in-place edit — follows the same pipeline
shape but does not extend `RenameCommandBase`: `CreatePipeCommand` implements `ICommand<TResult>`
directly, since its payload (network, part match, diameter, start/end geometry) doesn't fit the
rename shape.

```text
MCP Tool (CreatePipeTool)                Civil3D.Tools.Editing
  → CreatePipeRequest → CreatePipeCommand (endpoint computed from start + length + direction)
CommandDispatcher (validation → permission → confirmation → progress)
  → TransactionPipeline (begin → document lock → handler → commit | rollback)
    → CreatePipeCommandHandler (undo unit)
      → ICreatePipeService (network existence + PartCreated event)
        → IPipeCreateRepository (Autodesk part resolution + Network.AddLinePipe)
  → protocol response (CreatePipeResult)
```

**Part resolution** is the operation's one Autodesk-specific step, isolated entirely in
`AutodeskPipeCreateRepository`:

1. Open the target network for write (`Database.GetObjectId` + `Transaction.GetObject`, same
   pattern as the rename repositories).
2. Read the network's `PartsList` and call `GetPartFamilyIdsByDomain(DomainType.Pipe)` to get
   every pipe part family already assigned to it.
3. Match `CreatePipeSpecification.PartFamilyMatch` (case-insensitive substring) against each
   family's `Description`. Zero or more-than-one matches throw `DomainException(PartNotFound, …)`
   listing the available/ambiguous family descriptions — the tool never guesses.
4. Call `Network.AddLinePipe(familyId, sizeId, line, ref newPipeId, applyRules: true)` with any
   size from the matched family as the creation seed.
5. Call `Pipe.ResizeByInnerDiameterOrWidth(diameterMeters, useClosestSize: true)` on the new pipe
   — Civil 3D's own native snapping selects the closest size to the requested diameter, so the
   repository never has to parse catalog size names.

`CreatePipeTool` builds the default `PartFamilyMatch` from the discrete `Material`/`Sdr`/
`PressureClassBar` request fields (for example `HDPE` + `17` + `10` → `"HDPE SDR17 PN10"`) unless
the caller supplies an explicit override. The pipe is always **horizontal**: the tool computes the
end point from `StartEasting`/`StartNorthing` + `LengthMeters` at `DirectionDegrees` (0 =
+Easting axis, counter-clockwise), holding elevation constant — sloped pipes are out of scope for
this command.

The network itself is never created implicitly — `create_pipe` requires an existing network name
(discoverable via `list_pipe_networks`) and fails with `E_OBJECT_NOT_FOUND` otherwise, exactly
like rename's `E_OBJECT_NOT_FOUND` for a missing alignment/surface id.

| Failure | Domain code | Protocol code |
|---|---|---|
| Network missing | `EntityNotFound` | `E_OBJECT_NOT_FOUND` |
| Empty network name / part match / non-positive diameter or length | validator | `E_VALIDATION_FAILED` |
| No / ambiguous pipe part family match | `PartNotFound` | `E_VALIDATION_FAILED` |
