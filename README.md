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

1. Install the **bridge**: copy the `Civil3D.Bridge.Bundle` folder from a release
   into `%APPDATA%\Autodesk\ApplicationPlugins\` and restart Civil 3D.
2. Install the **server**: `npm install -g autodesk-mcp-server` (or
   `npx -y autodesk-mcp-server`).
3. Point your AI client at the server (ready-made configs in `examples/clients/`).

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

Version `1.0.0-rc.1` (RC1) with the Phase 9 production-hardening work complete
(see `docs/PRODUCTION-HARDENING.md` and `docs/RELEASE-1.0.0-RC2.md`). Tags follow
`v<semver>`; see `docs/ReleaseProcess.md`.

## License

MIT
