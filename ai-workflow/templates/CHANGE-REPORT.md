# Change Report — <task title>

Produced in Phase 4 of [02-WORKFLOW.md](../02-WORKFLOW.md).

---

## What changed

One paragraph, in user-visible terms. What can someone do now that they could not
do before, or what stopped being broken.

## Files touched

| File | Change |
| --- | --- |
| `…` | … |

**Plan misses** (files touched that were not in the plan, and why):
> none / …

---

## Verification

State the confidence level for each claim using the exact words from
[05-VERIFICATION.md](../05-VERIFICATION.md) §7.

| Claim | Level | Evidence |
| --- | --- | --- |
| Builds with zero warnings | Verified | `dotnet build AutodeskMcp.Core.slnx -c Release` → Build succeeded |
| Unit tests pass | Verified | `dotnet test …` → Passed: N, Failed: 0 |
| Behaves correctly in Civil 3D | Compiles only | not run against a live Civil 3D |

### Command output

```text
<paste the real tail of each command — pass/fail counts included>
```

### Not verified

- …
- What a human should check manually in Civil 3D before trusting this: …

---

## Contract impact

- Tool names added/changed: …
- DTO fields added/changed: …
- Error codes added: …
- Breaking for existing clients: yes / no — …
- Docs updated: …
- `CHANGELOG.md` updated: yes / no

---

## Review checklist result

Result of [08-REVIEW-CHECKLIST.md](../08-REVIEW-CHECKLIST.md):

```
Scope … Architecture … Contracts … Code quality … Tests … Verification … Docs … Honesty …
Overall: SHIP / NEEDS WORK
```

Blocking issues: none / …

---

## Honest assessment

- **Weakest part of this change:** …
- **Assumptions I made instead of asking:** …
- **Known gaps / TODOs left behind:** …
- **What I would do with more time:** …
