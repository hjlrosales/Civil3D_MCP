# Autodesk MCP Platform

Let any MCP-compatible AI assistant read and edit **Autodesk Civil 3D** drawings
through tools - alignments, surfaces, corridors, pipe networks, quantity takeoff,
cut/fill and more.

```
AI client (Claude Desktop, Cursor, VS Code, Cline, ...)
        |  Model Context Protocol (stdio)
        v
Autodesk.MCP.Server   (TypeScript / Node.js - product-agnostic MCP server)
        |  JSON-RPC 2.0 over a local Windows named pipe
        v
Civil3D.Bridge        (C# / .NET 8 plugin loaded inside Civil 3D)
        |  Autodesk .NET API (in-process)
        v
Autodesk Civil 3D 2025 / 2026
```

- **No network**: everything runs on local named pipes, ACL'd to the current user.
- **Dynamic discovery**: the server learns the entire tool catalog from the bridge
  at handshake time - no tool is hardcoded, and future products plug in with zero
  server changes.
- **Safe editing**: editing tools run in transactions and require confirmation.

## Quick start

1. Install the **bridge**: copy the `Civil3D.Bridge.Bundle-<version>.bundle` folder
   from a release into `%APPDATA%\Autodesk\ApplicationPlugins\` and restart Civil 3D.
   It loads automatically — `NETLOAD` is not part of normal use.
2. Install the **server**: `npm install -g autodesk-mcp-server` (or
   `npx -y autodesk-mcp-server@latest`).
3. Point your AI client at the server (ready-made configs in `examples/clients/`).

Civil 3D and your AI client can be started in any order, and either can be restarted
without touching the other: the server watches the endpoint registry continuously and
pushes tool-list updates as bridges come and go. See
[docs/Installation.md](docs/Installation.md#how-vs-code-connects-to-civil-3d) for the
full connection walkthrough, including how to diagnose "Discovered 0 tools".

See `docs/QuickStart.md` and `docs/Installation.md` for the full walkthrough.

## Documentation

| Guide | Contents |
| --- | --- |
| `docs/QuickStart.md` | 5-minute setup + first prompts |
| `docs/Installation.md` | bridge bundle + npm install, upgrade, uninstall |
| `docs/Configuration.md` | server/bridge options, env vars, logging, multi-bridge |
| `docs/Troubleshooting.md` | diagnosis by symptom |
| `docs/DeveloperGuide.md` | build, test, benchmark, release from source |
| `docs/ReleaseProcess.md` | versioning, tags, release pipeline |
| `docs/Compatibility.md` | supported products/clients/protocol guarantees |
| `docs/RELEASE-VALIDATION.md` | acceptance checklist before announcing |
| `docs/PERFORMANCE-BENCHMARKS.md` | measured metrics + how to run them |
| `docs/ARCHITECTURE.md` | full design, ADRs, sequence diagrams |
| `docs/FAQ.md` | frequently asked questions |
| `docs/EDITING-TOOLS.md` | editing commands: rename, create/update/delete pipe, pipe networks |
| `docs/COMMAND-FRAMEWORK.md` | write-transaction pipeline, validation, confirmation, events |
| `docs/TOOL-DEVELOPMENT.md` | the standard for building/registering/testing a tool |
| `docs/DOMAIN-LAYER.md` | domain projects, services, repositories, DTO conventions |
| `docs/QUERY-FRAMEWORK.md` | filtering/sorting/pagination for list/search tools |
| `docs/WORKFLOW-FRAMEWORK.md` | multi-step engineering workflow framework |
| `docs/DRAWING-HEALTH-REPORT.md` | `drawing_health_report` workflow |
| `docs/PROJECT-SUMMARY-REPORT.md` | `project_summary_report` workflow |
| `docs/DESIGN-VALIDATION.md` | `design_validation_report` rules & engine |
| `docs/QUANTITY-TAKEOFF.md` | `quantity_takeoff_report` workflow |
| `docs/SURFACE-COMPARISON.md` | `surface_comparison_report` workflow |
| `docs/CUT-FILL.md` | `cut_fill_report` workflow |
| `docs/CORRIDOR-ANALYSIS.md` | `corridor_analysis_report` workflow |
| `docs/LANDXML-EXPORT.md` | `export_landxml` workflow |

## Examples

- `examples/clients/` - Claude Desktop, VS Code, Cursor, Cline configurations
- `examples/config/` - server/bridge configs, env vars, logging, multi-bridge
- `examples/prompts/` - typical prompts by workflow
- `examples/workflows/` - end-to-end session transcripts
- `examples/json-rpc/` - real wire messages (NDJSON over the pipe)

## Development

```bash
npm run quality          # full local quality gate (mirrors CI)
```

See `docs/DeveloperGuide.md` and `docs/Contributing.md`.

## Releases

Version `1.0.0` (GA) with the Phase 9 production-hardening work complete
(see `docs/PRODUCTION-HARDENING.md` and `docs/RELEASE-1.0.0-FINAL.md`). Tags follow
`v<semver>`; see `docs/ReleaseProcess.md`.

## License

MIT - see [LICENSE](LICENSE).
