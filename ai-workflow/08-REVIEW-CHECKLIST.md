# 08 — Review Checklist

Run this before any change is called done — by the model on itself, and by the
human before merging. Answer **PASS / FAIL / N/A with one line of evidence**
(a `file:line`, or command output). An unanswered item is a FAIL.

---

## 1. Scope

- [ ] Every changed file appears in the plan.
- [ ] No file in the plan was left unchanged (or the omission is explained).
- [ ] No renames, reformatting or refactors that the task did not require.
- [ ] No new top-level directory, doc, or "summary" file that was not requested.
- [ ] The diff contains one logical change.

## 2. Architecture

- [ ] No Autodesk reference added to `Autodesk.Mcp.Shared`, `Autodesk.Mcp.Sdk`,
      `Civil3D.Tools.Abstractions`, or any `Civil3D.Domain.*` project.
- [ ] No Civil 3D / product concept added anywhere under `src/server/`.
- [ ] No MCP or AI concept added to `Civil3D.Bridge`.
- [ ] No manual tool registration (no list, switch or array of tools edited).
- [ ] Tool classes contain no direct Autodesk API calls — they go through an
      Autodesk-free service contract.
- [ ] Dependency direction unchanged; no new project reference pointing "up".
- [ ] No network listener, socket, or outbound call introduced.

## 3. Contracts

- [ ] No shipped tool name or DTO field renamed or removed.
- [ ] `[McpTool]` `Version` bumped if schema or behaviour changed.
- [ ] New/changed fields have units in the XML doc where units apply.
- [ ] Error codes reuse existing `E_*` values; no near-duplicates invented.
- [ ] Protocol changes landed on **both** C# and TypeScript sides with round-trip
      tests and a version bump.
- [ ] No version number hand-edited (`npm run quality:check` is green).

## 4. Code quality

- [ ] Builds with **zero warnings** (`TreatWarningsAsErrors` is on under `src/`).
- [ ] `dotnet format --verify-no-changes` clean.
- [ ] Nullable annotations correct; no `!` or `#pragma warning disable` added to
      silence the compiler.
- [ ] Public types/members and DTO properties have XML docs.
- [ ] DTOs are immutable records with `init`-only properties.
- [ ] Dependencies injected via constructor; nothing `new`-ed up internally.
- [ ] Read paths use read-only transactions; write paths go through the command
      pipeline with lock, validation, confirmation and rollback.
- [ ] No secret, absolute local path, or machine-specific value committed.

## 5. Tests

- [ ] Happy path covered.
- [ ] Each failure mode named in the plan has its own test.
- [ ] Cancellation covered where `SupportsCancellation = true`.
- [ ] Tests run headless — none require a live Civil 3D.
- [ ] No test deleted, skipped or weakened to make the suite green.
- [ ] Tests assert on returned DTOs / events, not on mock call counts.
- [ ] Test names follow `Method_Scenario_ExpectedOutcome`.

## 6. Verification

- [ ] The commands from [05-VERIFICATION.md](05-VERIFICATION.md) that apply were
      actually run.
- [ ] Real output pasted, including pass/fail counts.
- [ ] `npm run quality` green (or the specific failure reported, not hidden).
- [ ] Confidence level stated per claim: **Verified / Compiles only / Unverified**.
- [ ] Anything that needs live Civil 3D verification is called out as outstanding.

## 7. Documentation

- [ ] User-visible behaviour change reflected in the relevant `docs/` file.
- [ ] New tool documented where the existing tools of that domain are documented.
- [ ] `CHANGELOG.md` updated for user-visible changes.
- [ ] Every command/path referenced in new prose actually exists.

## 8. Honesty

- [ ] Nothing is claimed as tested that was not executed.
- [ ] Every assumption made in place of a question is written down.
- [ ] Known gaps, shortcuts and TODOs are listed rather than left to be found.
- [ ] The weakest part of the change is named explicitly.

---

### Verdict

```
Scope        PASS / FAIL
Architecture PASS / FAIL
Contracts    PASS / FAIL
Code quality PASS / FAIL
Tests        PASS / FAIL
Verification PASS / FAIL
Docs         PASS / FAIL
Honesty      PASS / FAIL

Overall: SHIP / NEEDS WORK
Blocking issues: <list, or "none">
```

Any FAIL in Architecture, Contracts, Tests, or Honesty is **blocking**.
