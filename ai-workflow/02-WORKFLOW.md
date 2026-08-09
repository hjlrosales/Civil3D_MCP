# 02 — The Workflow

**This is mandatory. Do not skip phases. Do not reorder them.**

Five phases: **Orient → Plan → Implement → Verify → Report.**
Each phase has an exit gate. If the gate fails, you stay in the phase — you do
not proceed and hope.

---

## Phase 0 — Orient (read before you think)

Goal: know what already exists so you extend it instead of reinventing it.

Do, in this order:

1. Restate the task in one sentence. If you cannot, the task is ambiguous — ask
   one clarifying question and stop.
2. Find the nearest existing thing that already does something similar.
   - New tool? Find the most similar existing tool in `src/bridges/Civil3D.Tools.*`.
   - New command? Find the most similar handler in `.../Commands/`.
   - Server change? Find the module in `src/server/.../src/` that owns the concern.
3. Read that thing **completely**, plus its test file.
4. Read the relevant doc from the table in [01-PROJECT-MAP.md](01-PROJECT-MAP.md) §7.

**Gate 0 — you may not continue until you can name:**
- the existing file you are modelling the change on,
- the assembly/module the new code belongs in,
- the test project that will cover it.

If you cannot name all three, keep reading. Guessing here is the single largest
source of bad output in this repo.

---

## Phase 1 — Plan (write it down, in the chat, before editing)

Produce the plan using [templates/PLAN.md](templates/PLAN.md). It is short — half
a page — but every field is required.

A plan must contain:

- **Goal**: one sentence, in user-visible terms.
- **Files to create/modify**: the full list, with a one-line reason each. If you
  discover a sixth file during implementation, that is a plan miss — note it in
  the report.
- **Contract changes**: any new/changed DTO field, tool name, error code, or
  protocol message. Say "none" explicitly if none.
- **Test plan**: the named test cases, including the failure cases.
- **Risk**: what could break that is outside the files listed.
- **Out of scope**: what you are deliberately not doing.

**Gate 1 — the plan is rejected if it:**
- touches more than one logical concern,
- says "and update related files as needed",
- has no failure-case tests,
- changes a public tool name or DTO field without saying so under Contract changes.

Wait for approval on any plan that changes a contract, deletes code, or touches
`eng/`, `packaging/`, or `.github/`. Otherwise proceed.

---

## Phase 2 — Implement

Rules of engagement:

1. **Follow the local pattern, not your habits.** The file you read in Phase 0 is
   the template. Match its structure, naming, XML-doc density and error handling.
2. **Smallest change that fully solves the task.** No opportunistic refactors, no
   renames "while we're here", no reformatting untouched lines.
3. **Write the test in the same pass**, not after. If the test is hard to write,
   the design is wrong — go back to Phase 1.
4. **No new dependencies** without approval. This repo has a deliberately small
   dependency surface.
5. **No new files outside the plan.** If you need one, amend the plan out loud
   first.
6. **Never edit generated or versioned-by-script values**: `Directory.Build.props`
   versions, `package.json` versions, `PackageContents.xml` `AppVersion`. Run
   `npm run sync:version` instead.
7. If you get stuck for two attempts on the same error, **stop and report the
   error** rather than trying a third unrelated approach. Thrashing produces the
   worst code in the repo.

**Gate 2 — implementation is complete when:**
- every file in the plan is changed,
- the change compiles with zero warnings,
- tests exist for the happy path **and** each failure mode named in the plan.

---

## Phase 3 — Verify (evidence, not belief)

Run the commands in [05-VERIFICATION.md](05-VERIFICATION.md) that apply to what
you touched. Minimum:

| You touched | You must run |
| --- | --- |
| Any C# under `src/` | `dotnet build AutodeskMcp.Core.slnx -c Release` + `dotnet test AutodeskMcp.Core.slnx -c Release` |
| Bridge / tool assemblies | above, plus `dotnet build AutodeskMcp.slnx -c Release` if Civil 3D is installed |
| TypeScript server | `npm run typecheck:server && npm run lint:server && npm run test:server` |
| Protocol / wire contract | both sides above, plus `npm run test:e2e` |
| Versions, packaging, release | `npm run quality:check` |
| Anything, before declaring done | `npm run quality` |

**Gate 3 — you may not proceed to Report until you have pasted real command
output.** Not a summary of it. Not "tests pass". The actual tail of the output,
including the pass/fail counts.

If something fails and you cannot fix it, that is a legitimate outcome — carry it
into the report as a known failure. Silently dropping a failing test is the worst
possible move.

---

## Phase 4 — Report

Produce [templates/CHANGE-REPORT.md](templates/CHANGE-REPORT.md). Then run
[08-REVIEW-CHECKLIST.md](08-REVIEW-CHECKLIST.md) against your own change and
state the result.

The report must distinguish three levels of confidence, explicitly:

- **Verified**: a command was run and passed; output shown.
- **Compiles only**: builds and unit tests pass, but the behaviour was never
  exercised against a running Civil 3D.
- **Unverified**: reasoning only.

Most bridge changes land at *compiles only* because Civil 3D is not available in
CI. That is fine and expected. **Claiming "verified" for one of those is not.**

---

## Session hygiene

- **One task per session.** Start a new conversation for the next task; long
  sessions drift and the model starts contradicting its own earlier decisions.
- **Re-anchor after ~20 exchanges**: re-paste `03-RULES.md` and the current plan.
- **When the model goes off the rails**, do not argue with it. Restart the session
  with a tighter plan. Correcting a confused context costs more than restarting.
- **Never let a model run destructive commands** (`git reset --hard`, `git clean`,
  deleting directories) without reading the target first.

---

## Commit discipline

- One logical change per commit; message prefixed by area:
  `server:`, `bridge:`, `domain:`, `packaging:`, `docs:`.
- Never commit `bin/`, `obj/`, `dist/`, `node_modules/`, `artifacts/`.
- Commit only when asked. Do not amend someone else's commit.
- The subject line says *what changed and why*, not "fix" or "updates".
