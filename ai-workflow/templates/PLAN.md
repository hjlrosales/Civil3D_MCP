# Plan — <task title>

Produced in Phase 1 of [02-WORKFLOW.md](../02-WORKFLOW.md). Half a page. Every
field required; write "none" rather than omitting one.

---

**Goal** (one sentence, user-visible terms)
> …

**Modelled on** (the existing file this change follows)
> `path/to/ExistingThing.cs` — and its test `tests/…/ExistingThingTests.cs`

**Assumptions** (anything I decided instead of asking)
> …

---

## Files

| File | Create / Modify | Why |
| --- | --- | --- |
| `src/…` | Create | … |
| `src/…` | Modify | … |
| `tests/…` | Create | … |

If a file appears during implementation that is not on this list, note it in the
change report as a plan miss.

---

## Contract changes

- New/changed tool name: … (or none)
- New/changed DTO fields (with units): … (or none)
- New/changed error codes: … (or none)
- `[McpTool]` `Version` bump needed: yes / no
- Protocol change (dual-sided C# + TS): yes / no
- Breaking for existing clients: yes / no — if yes, how

---

## Test plan

| Test | Asserts |
| --- | --- |
| `Op_HappyPath_…` | … |
| `Op_NoActiveDocument_…` | throws `E_NO_ACTIVE_DOCUMENT` |
| `Op_InvalidInput_…` | throws `E_VALIDATION_FAILED` |
| `Op_UnknownId_…` | throws `E_OBJECT_NOT_FOUND` |
| `Op_Cancelled_…` | honours the cancellation token |

Test project: `tests/…`

---

## Verification plan

Commands I will run (from [05-VERIFICATION.md](../05-VERIFICATION.md)):

- …

Expected confidence level on completion: **Verified / Compiles only**.
What will remain unverifiable without a live Civil 3D: …

---

## Risk

- What could break outside the listed files: …
- Rollback: …

---

## Out of scope

- …
