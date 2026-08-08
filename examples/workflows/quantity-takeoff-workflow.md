# Workflow: Quantity takeoff

## 1. Scope the corridor

**Prompt:** "Set up a quantity takeoff for corridor 'Road Corridor'."

**Tool calls:** `quantity_takeoff { corridor: "Road Corridor", criteria: "pavement" }`
-> line items grouped by code: AC, AB, subgrade.

## 2. Review the numbers

**Prompt:** "Show the quantities per material as a table."

**Tool calls:** `quantity_report { takeoffId: "..." }` -> area/volume per material per
station range.

## 3. Export the report

**Prompt:** "Export the takeoff to CSV."

**Tool calls:** `quantity_export { takeoffId: "...", format: "csv", path: "..." }` ->
file written.
