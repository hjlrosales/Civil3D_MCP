# Workflow: Site grading & earthwork

A transcript-style example of a typical session: inspect -> compare -> analyse ->
export. Tool names are illustrative of the live catalog.

## 1. Understand the site

**Prompt:** "What surfaces exist in this drawing?"

**Tool calls:** `list_surfaces` -> returns `EG` (existing ground), `FG` (finished grade).

## 2. Inspect the proposed grading

**Prompt:** "Summarize the extents and stats of FG."

**Tool calls:** `surface_info { name: "FG" }` -> area, min/max elevation, bounds.

## 3. Compare and compute earthwork

**Prompt:** "Run a cut/fill analysis between EG and FG and summarize."

**Tool calls:**

- `calculate_cut_fill { existingSurface: "EG", proposedSurface: "FG" }`
  -> net volume, cut, fill, balanced areas (long-running; progress + cancelable).
- `earthwork_report { ... }` -> structured report.

**Result you would see:** "Net cut 12,450 m3; fill 8,100 m3; balance option: import
4,350 m3."

## 4. Validate

**Prompt:** "Check the grading against the design validation rules."

**Tool calls:** `design_validation_run { surface: "FG" }` -> violations list with
severities.

## 5. Export

**Prompt:** "Export EG and FG to LandXML."

**Tool calls:** `export_landxml { objects: ["EG", "FG"], outputPath: "..." }` -> saved
file path.
