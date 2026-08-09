# AI Engineering Workflow

Operating manual for any AI assistant (DeepSeek, Claude, GPT, Cursor, …) doing
engineering work in this repository.

**Why this folder exists:** the codebase has strict architectural invariants
(the bridge knows nothing about MCP; the server knows nothing about Autodesk;
tools are discovered by reflection, never registered by hand). An assistant that
does not know these rules produces code that compiles, looks plausible, and is
wrong. These documents encode the rules so output is correct on the first pass
instead of the fourth.

---

## How to use this with a model

Models degrade when given the whole repo. Give them **exactly one context pack**
for the task at hand. Paste the listed files into the chat (or point the tool at
them), then paste the task.

| You want to… | Load these, in order |
| --- | --- |
| Anything at all (always) | `02-WORKFLOW.md`, `03-RULES.md` |
| Understand where code lives | `01-PROJECT-MAP.md` |
| Add or change a Civil 3D tool | `01-PROJECT-MAP.md`, `04-PATTERNS.md`, `docs/TOOL-DEVELOPMENT.md` |
| Add or change an editing (write) tool | above **plus** `docs/COMMAND-FRAMEWORK.md`, `docs/EDITING-TOOLS.md` |
| Change the MCP server (TypeScript) | `01-PROJECT-MAP.md`, `docs/MCP-SERVER.md` |
| Change the wire protocol | `01-PROJECT-MAP.md`, `docs/ARCHITECTURE.md` §2 |
| Fix a bug | `02-WORKFLOW.md`, `05-VERIFICATION.md`, plus the failing test file |
| Review / finish up | `05-VERIFICATION.md`, `08-REVIEW-CHECKLIST.md` |

Do **not** paste `06-ANTIPATTERNS.md` at the start of a task — paste it when the
model has already produced something questionable, alongside the specific
mistake.

---

## The files

| File | What it is |
| --- | --- |
| [01-PROJECT-MAP.md](01-PROJECT-MAP.md) | Where everything lives, layer boundaries, the one-paragraph mental model |
| [02-WORKFLOW.md](02-WORKFLOW.md) | The mandatory five-phase loop with stop gates |
| [03-RULES.md](03-RULES.md) | Hard constraints. Violating one is a defect, not a style opinion |
| [04-PATTERNS.md](04-PATTERNS.md) | Copy-paste-shaped patterns for tools, DTOs, commands, tests |
| [05-VERIFICATION.md](05-VERIFICATION.md) | Exact commands to prove a change works, and what counts as proof |
| [06-ANTIPATTERNS.md](06-ANTIPATTERNS.md) | The specific failure modes seen in this repo, with the fix |
| [07-PROMPT-TEMPLATES.md](07-PROMPT-TEMPLATES.md) | Fill-in-the-blank prompts for the recurring task types |
| [08-REVIEW-CHECKLIST.md](08-REVIEW-CHECKLIST.md) | Pass/fail gate before saying "done" |
| [templates/PLAN.md](templates/PLAN.md) | The plan a model must produce before touching code |
| [templates/CHANGE-REPORT.md](templates/CHANGE-REPORT.md) | The report a model must produce after touching code |

---

## The short version

If you read nothing else, enforce these five:

1. **Plan before code.** No edits until a written plan names every file to touch.
2. **One logical change per session.** Not "add the tool and refactor the
   dispatcher and update the docs framework."
3. **Tests are part of the change**, written in the same pass, not offered as an
   afterthought.
4. **`npm run quality` is the arbiter.** Not the model's opinion that it looks right.
5. **Report honestly.** "Builds, unit tests pass, not tested against live Civil 3D"
   is a good report. "Done!" is not.

---

## Relationship to `docs/`

`docs/` describes **the product** — architecture, tool catalog, install, release.
`ai-workflow/` describes **how to work on it**. When they disagree, `docs/` wins
on facts about the system; this folder wins on process. Fix the disagreement
rather than living with it.
