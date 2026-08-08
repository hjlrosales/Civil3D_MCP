# FAQ

## General

**What is this?**
An MCP server (`autodesk-mcp-server`) plus an in-Civil-3D plugin (`Civil3D.Bridge`)
that let any MCP-compatible AI assistant read and edit Civil 3D drawings through
tools - alignments, surfaces, corridors, pipes, quantity takeoff, and more.

**Is it specific to Autodesk?**
The server is product-agnostic; it discovers whatever bridge is running. Only the
bridge (and the drawing tools project) reference Autodesk APIs.

**Does it touch the network?**
No. Everything runs on local Windows named pipes; nothing listens on a network
port.

**Which Civil 3D versions?**
2025 and 2026 (compiled against the 2025 SDK).

## Installation

**Do I need the AutoCAD SDK to install the bundle?**
No - the bundle is prebuilt. The SDK is only needed to build the bridge from source.

**Why `npx -y autodesk-mcp-server`?**
`npx` downloads and runs the latest published package without a permanent install.
Remove `-y` to skip the download confirmation.

**How do I update?**
See `docs/Installation.md` (upgrade section). Replace the bundle folder and
reinstall/relaunch the npm package.

## Usage

**My client shows no tools.**
Most often the bridge is not loaded (Civil 3D not running with the bundle) or the
endpoint directory is wrong. Follow `docs/Troubleshooting.md`.

**Tools that edit the drawing ask for confirmation.**
Yes - by design. Editing tools require `confirm: true` (or a client elicitation
flow). Read-only tools never do.

**Can two people use it at once?**
Named pipes are per-user and per-process. Each Windows user session has its own
endpoint registry; the server connects to the bridges it can see.

**What happens when I close Civil 3D?**
The bridge removes its endpoint descriptor; the server marks the bridge offline and
reconnects (with backoff) when Civil 3D returns.

## Compatibility

**Does it work with Claude Desktop / Cursor / VS Code / Cline?**
Yes - any MCP stdio client. Ready-made configs: `examples/clients/`.

**Does it work on macOS/Linux?**
The server requires Windows named pipes. It runs on Windows only (the pipes are
local to the machine hosting Civil 3D).

**Which Node version?**
>= 20.

## Performance

**Is the named pipe fast enough for interactive use?**
Yes - see `docs/PERFORMANCE-BENCHMARKS.md` for measured round-trip latencies and
throughput.

**Large projects?**
Read-only queries stream results in one envelope; heavy operations (corridor
rebuild, cut/fill) declare longer timeouts in their manifests and can be cancelled.
See `docs/Troubleshooting.md` (E_TIMEOUT).

## Troubleshooting

**Where are the logs?**
Server: structured JSON on stderr. Bridge: `%LOCALAPPDATA%\AutodeskMcp\logs\`.

**It still does not work.**
Follow `docs/Troubleshooting.md` end to end, then open an issue with the logs.
