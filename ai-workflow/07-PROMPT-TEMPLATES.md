# 07 — Prompt Templates

Fill in the blanks and paste. Every template already carries the workflow and the
gates, so the model cannot skip them.

---

## T0 — Session preamble (paste once, at the start of every session)

```
You are working in the Autodesk MCP Platform repository (C# bridge + TypeScript
MCP server for Autodesk Civil 3D).

Read and follow these, in full, before doing anything:
  - ai-workflow/02-WORKFLOW.md   (the mandatory Orient → Plan → Implement → Verify → Report loop)
  - ai-workflow/03-RULES.md      (hard rules; violating one is a defect)

Non-negotiable:
  - No code until you have produced a written plan and I have not objected.
  - One logical change only.
  - Tests in the same pass as the code.
  - Report with real command output and an explicit confidence level
    (Verified / Compiles only / Unverified).
  - If you are unsure about a requirement, ask one question and stop.

Acknowledge by listing the five phases and then wait for my task.
```

---

## T1 — Add a read-only tool

```
TASK: Add a Civil 3D read-only tool `<tool_name>` that <what it returns>.

Context to read first:
  - ai-workflow/01-PROJECT-MAP.md
  - ai-workflow/04-PATTERNS.md  (patterns P1, P2, P4)
  - docs/TOOL-DEVELOPMENT.md
  - src/bridges/Civil3D.Tools.<Domain>/Tools/<ClosestExistingTool>.cs   ← model on this
  - tests/Civil3D.Tools.<Domain>.Tests/<ClosestExistingTool>Tests.cs

Requirements:
  - Assembly: src/bridges/Civil3D.Tools.<Domain>
  - Inputs: <fields, units, which are required>
  - Output DTO fields: <fields, units>
  - Failure modes and their error codes: <e.g. no active document → E_NO_ACTIVE_DOCUMENT>
  - Permission: ReadOnly. Risk: <Low|Medium>.

PHASE 1 ONLY: produce the plan using ai-workflow/templates/PLAN.md. Do not write
code yet.
```

---

## T2 — Add an editing (write) tool

```
TASK: Add the editing tool `<tool_name>` that <operation> on <entity>.

Context to read first:
  - ai-workflow/01-PROJECT-MAP.md
  - ai-workflow/04-PATTERNS.md  (pattern P3)
  - docs/COMMAND-FRAMEWORK.md
  - docs/EDITING-TOOLS.md
  - src/bridges/Civil3D.Tools.Editing/{Tools,Commands,Validators,Dtos}/<ClosestExisting>*
  - tests/Civil3D.Tools.Editing.Tests/<ClosestExisting>Tests.cs

Requirements:
  - Request fields: <fields, units, optionality>
  - Validation rules: <what must be rejected, and with which error code>
  - Result must report which changes were actually applied.
  - Confirmation: <required | not required>. Undo must be registered.
  - Error codes: unknown id → E_OBJECT_NOT_FOUND; nothing to change →
    E_VALIDATION_FAILED; Civil 3D rejection → E_TRANSACTION_FAILED.

Expected files (five): Dtos/<Op>Request.cs, Commands/<Op>Command.cs,
Commands/<Op>CommandHandler.cs, Validators/<Op>CommandValidator.cs,
Tools/<Op>Tool.cs, plus tests.

PHASE 1 ONLY: produce the plan. Do not write code yet.
```

---

## T3 — Fix a bug

```
BUG: <what happens> — expected <what should happen>.

Reproduction: <exact steps, tool call, or failing test name>
Observed output / error:
<paste it verbatim, including the error code and correlation id>

Environment: <Civil 3D version, server version, bridge version>

Do this in order:
1. DIAGNOSE ONLY. Name the file and line you believe is responsible and explain
   the mechanism. Do not propose a fix yet.
2. After I confirm the diagnosis: write a failing test that reproduces it.
3. Then the minimal fix.
4. Then run the relevant commands from ai-workflow/05-VERIFICATION.md and paste
   the real output.

Do not change anything outside the cause. If the root cause is elsewhere than the
symptom, say so before changing either.
```

---

## T4 — TypeScript server change

```
TASK: <change> in the MCP server.

Context to read first:
  - ai-workflow/01-PROJECT-MAP.md §2 (server layout)
  - docs/MCP-SERVER.md
  - src/server/Autodesk.Mcp.Server/src/<owning module>
  - src/server/Autodesk.Mcp.Server/test/<matching test file>

Hard constraint: the server must remain completely product-agnostic — no Civil 3D
concept, no tool name, no product branch may appear anywhere in src/server/. If
the change appears to require one, stop and tell me; the fix belongs in the bridge.

Verify with: npm run typecheck:server && npm run lint:server && npm run test:server
(and npm run test:e2e if transport, discovery or lifecycle is touched).
```

---

## T5 — Protocol / wire change

```
TASK: <protocol change>.

This is a DUAL-SIDED change. It is incomplete unless all of these land together:
  1. C#  — src/shared/Autodesk.Mcp.Shared
  2. TS  — src/server/Autodesk.Mcp.Server/src/protocol/
  3. Round-trip tests on BOTH sides
  4. Protocol version bump (breaking change → major)
  5. docs/ARCHITECTURE.md §2 updated

Read docs/ARCHITECTURE.md §2 and both protocol directories before planning.
State explicitly whether this is breaking, and what an old server talking to a new
bridge (and vice versa) will do.

PHASE 1 ONLY: produce the plan. This one needs my approval before any code.
```

---

## T6 — Review a change the model just made

```
Review your own change against ai-workflow/08-REVIEW-CHECKLIST.md.

For EVERY item: answer PASS, FAIL or N/A with one line of evidence — a file:line,
or a command output. Do not answer "PASS" for anything you did not actually check.

Then list, honestly:
  - anything you changed that was not in the plan,
  - anything you could not verify and why,
  - the weakest part of the change.
```

---

## T7 — Unstick a thrashing session

```
Stop. Do not try another fix.

Answer only these, in plain text, no code:
1. What exactly did you attempt (list each attempt and the error it produced)?
2. What is your current best hypothesis for the root cause?
3. What information would confirm or refute it?
4. What in the repo have you NOT read that is relevant?

Then wait.
```

---

## T8 — Documentation change

```
TASK: Update <doc> to reflect <change>.

Rules:
  - Match the voice of the surrounding document.
  - Every command, path and file reference must exist in the repo — verify each.
  - No new documents unless I asked for one.
  - If you find an existing statement that is now false, fix it and list it
    separately in your report.
```
