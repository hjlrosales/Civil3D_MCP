# Typical MCP Prompts

Copy-paste starters grouped by workflow. All of them work once the bridge is
running and the client has discovered the tools.

## Drawing inspection

- "What is in the current drawing? Summarize the layers, blocks, and whether any
  Civil 3D objects (alignments, surfaces, corridors) exist."
- "List all layers with their on/off and frozen status."
- "Report the drawing units, limits, and object counts."

## Alignments & profiles

- "List the alignments in this drawing. For each one, show the length and station
  range."
- "Summarize the profile for alignment 'Main Road': start/end stations and vertical
  curves."
- "Which alignments have no design profile yet?"

## Surfaces

- "List the surfaces and their extents. Which one covers the whole site?"
- "Compare the existing ground surface with the proposed surface and report the
  volume difference."
- "What is the maximum slope on surface 'EG'?"

## Corridors

- "List the corridors in this drawing and which alignments they follow."
- "Give me a cross-section station list for corridor 'Road Corridor' every 50 m."
- "Check corridor 'Road Corridor' for daylighting issues against surface 'EG'."

## Pipe networks & drainage

- "List the pipe networks and their parts (pipes + structures)."
- "Summarize the storm network: total pipe length, structure count, and any pipes
  with inverted slopes."
- "In the Storm network, create a horizontal 10 meter pipe, HDPE, 200 mm, SDR17,
  PN10, starting at easting 1000, northing 2000, elevation 95.5." (requires
  confirmation; the part is matched against the network's parts list, so the
  network must already have a matching HDPE/SDR17/PN10 pipe part family)

## Quantities & earthwork

- "Run a cut/fill analysis between 'EG' and 'FG' and summarize the results."
- "Produce a quantity takeoff for the corridor pavement layers."
- "Calculate the earthwork volume for the grading around building pad B-1."

## Editing (requires confirmation)

- "Create an alignment named 'Relief Route' along the polyline on layer 'ROAD-CL'."
- "Rename alignment 'Old Name' to 'New Name'."
- "Add a profile vertical curve to alignment 'Main Road' at station 1+250."
- "Create a horizontal 10 m HDPE pipe, 200 mm SDR17 PN10, in the Storm network."

## Export

- "Export the alignments, surfaces, and profiles to LandXML and save to the
  project folder."
- "Generate a drawing health report and summarize the warnings."

## Quality/health

- "Run the design validation rules on the current drawing and list violations."
- "Produce a project summary report for this drawing set."
