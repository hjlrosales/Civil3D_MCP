# PDF Underlay & Plot-to-PDF Spike — Engineering Report

**Date:** 2026-08-09
**Full write-up:** [docs/PDF-UNDERLAY-PLOT-SPIKE.md](../../docs/PDF-UNDERLAY-PLOT-SPIKE.md)

---

## Objective

Determine, with real evidence rather than a proposal, whether attaching a PDF underlay and
plotting a layout to PDF are reachable through the existing `ToolDispatcher →
IApplicationContext` execution path every shipped tool uses — as a prerequisite for scoping a
future `attach_pdf_underlay` / `export_pdf` MCP tool pair.

## Tests performed

1. Inspected the real execution path and the LandXML "honest not-supported" precedent to ground
   the spike in what actually ships.
2. Reflected over the real, installed Autodesk assemblies to confirm every PDF-underlay and Plot
   API type/member signature used — no API name or signature was guessed.
3. Built two spike tools in the exact shape of a production tool and confirmed they compile
   cleanly against the real SDK (first pass).
4. **With the user's explicit authorization**, after confirming the drawing was saved: rebuilt
   the bridge with the spike wired in, redeployed the plugin bundle, and restarted the actually
   running Civil 3D session. Created a throwaway blank drawing (a third spike tool,
   `spike_new_drawing`) so the real, already-open drawing was never touched. Called both spike
   tools for real, over the real production MCP server, through the real named pipe.
5. Found and fixed one real bug in the plot code (`RefreshLists` was called after, not before,
   `SetPlotConfigurationName`, causing a silent no-op with no exception and no output file), then
   re-ran the live trial and got a genuine pass, independently verified on disk.
6. Fully reverted every temporary change — bridge wiring, spike code, installed plugin bundle —
   and restarted Civil 3D once more to confirm the exact original state (35 tools,
   `bridgeVersion: "1.0.0"`).
7. Ran the repository quality gate.

## Actual results

| Capability | Result |
| --- | --- |
| PDF underlay attach | **PASS — VERIFIED LIVE** (first attempt, no code changes needed) |
| Plot-to-PDF | **PASS — VERIFIED LIVE** (after fixing one real ordering bug found live) |
| Production files after cleanup | **Unchanged** — `git status` shows no diff on any `src/`/`.slnx` file |
| Installed Civil 3D plugin after cleanup | **Restored** — confirmed live back to the exact pre-spike state |

## Evidence

- **PDF underlay attach**, live, on a throwaway drawing:
  `{"success": true, "stage": "complete", "definitionHandle": "F2E", "referenceHandle": "F2F", "verifiedInDrawing": true}` —
  `verifiedInDrawing` means the object was re-read by id in a fresh transaction after commit, not
  just that the write transaction didn't throw.
- **Plot-to-PDF**, live: first attempt completed every stage with **no exception** but produced
  **no file** (`success: false`, independently confirmed missing on disk) — a genuine, precise
  FAIL, not a crash. Root cause found (`RefreshLists`/`SetPlotConfigurationName` order) and
  fixed. Second attempt: `{"success": true, "fileSizeBytes": 6906, "appliedDeviceName": "DWG To PDF.pc3"}`,
  independently confirmed on disk as a real, valid PDF (`%PDF-1.7` header).
- Full stage-by-stage detail, the exact reflected API signatures, and the final corrected source
  for all three spike tools: see the linked full write-up's appendix.

## Production recommendation

- **PDF Underlay: IMPLEMENT.** No architectural blocker found; ready to scope as a real tool
  following pattern P3 (editing tool through the command pipeline).
- **Plot-to-PDF: IMPLEMENT.** No architectural blocker found; the only issue was a fixable
  sequencing bug in this spike's own code, not a platform limitation. A production tool should
  make the plot device configurable and report the device-not-found case as a structured,
  honest failure (the exact silent-no-op bug this spike caught) rather than repeating it.

Both capabilities cleared the bar the LandXML export precedent did not: no live interactive
document context was required, and both ran successfully through the platform's standard
background-tool execution path.

## Tests / quality gate

```
dotnet build AutodeskMcp.slnx -c Release   -> Build succeeded, 0 warnings, 0 errors
                                               (spike fully removed; production-only tree)
git status                                 -> no diff on any src/, tests/, eng/, packaging/,
                                               .github/, or .slnx file
```

No existing test was modified, skipped, or weakened.

## Remaining limitations

- Neither production tool has actually been built yet — this spike proves reachability and the
  correct API shape, not a finished, tested, error-mapped implementation.
- The `RefreshLists`-before-`SetPlotConfigurationName` fix was found for this one code path; a
  real implementation should still be tested against a machine where `"DWG To PDF.pc3"` is
  absent, to confirm the honest-failure behavior recommended above.
- A real, unresolved production-readiness gap was found as a side effect: the shipped bridge
  bundle is unsigned, and Windows/AutoCAD shows a blocking "unsigned executable" trust dialog on
  first load after any update — worth its own investigation before this platform has real users,
  independent of the PDF work.

## Next recommended step

Scope and implement `attach_pdf_underlay` and `export_pdf` as real, production tools following
this repo's standard workflow (`ai-workflow/02-WORKFLOW.md`): a written plan, the Autodesk-free
service-contract split (rule A5), proper `E_*` validation and error mapping in place of the
spike's diagnostic catch-everything style, and headless tests per pattern P4 — using the
live-verified API sequences in `docs/PDF-UNDERLAY-PLOT-SPIKE.md` as the grounded starting point.
