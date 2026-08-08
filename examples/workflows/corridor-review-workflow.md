# Workflow: Corridor review

## 1. Find corridors

**Prompt:** "List the corridors and what they follow."

**Tool calls:** `list_corridors` -> corridor name, baseline alignment, assembly set.

## 2. Examine a corridor

**Prompt:** "Analyze corridor 'Road Corridor'."

**Tool calls:** `corridor_analyze { corridor: "Road Corridor" }` -> regions, stations,
assemblies, subassembly usage.

## 3. Check stations

**Prompt:** "Give me cross-section data every 50 m for the whole corridor."

**Tool calls:** `corridor_section_list { corridor: "Road Corridor", interval: 50 }`
-> station list with geometry summaries.

## 4. Look for issues

**Prompt:** "Check for daylighting conflicts against EG."

**Tool calls:** `corridor_daylight_check { corridor: "Road Corridor", surface: "EG" }`
-> conflict locations and severity.
