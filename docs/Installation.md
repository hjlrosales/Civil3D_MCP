# Installation

This guide installs the Autodesk MCP Platform: the **Civil3D.Bridge** plugin inside
Autodesk Civil 3D and the **Autodesk.MCP.Server** MCP server for your AI client.

No source code editing is required. Everything ships prebuilt as a bundle and an npm
package.

---

## 1. Prerequisites

| Component | Requirement |
| --- | --- |
| Operating system | Windows 10/11 x64 |
| Autodesk product | Civil 3D 2025 or 2026 (full install, not just the Object Enabler) |
| Node.js | >= 20 LTS ([nodejs.org](https://nodejs.org)) |
| AI client | Claude Desktop, Cursor, VS Code, Cline, or any MCP stdio client |

The bridge is a managed plugin that loads into Civil 3D via the standard Autodesk
plugin loader (NETLOAD under the hood) - **no** AutoCAD SDK is required on the
target machine.

---

## 2. Install the Bridge bundle

### Option A - per-user install (recommended for individual machines)

1. Download `Civil3D.Bridge.Bundle-<version>.zip` from the release.
2. Extract it, then copy the whole `Civil3D.Bridge.Bundle-<version>` folder into:
   ```
   %APPDATA%\Autodesk\ApplicationPlugins\
   ```
   so the folder contains `PackageContents.xml` directly inside it.
3. Fully close and restart Civil 3D. The Autodesk plugin loader detects the bundle
   and loads the bridge automatically.

Verify:

```
%LOCALAPPDATA%\AutodeskMcp\endpoints\
```

should now contain a `Civil3D-<pid>.json` descriptor file, and

```
%LOCALAPPDATA%\AutodeskMcp\logs\civil3d-bridge-20260808.log
```

should contain `Civil 3D Bridge initialized: Civil3D.Bridge (pipe ...)`.

> **Alternative install (from a source checkout):**
> `node eng/scripts/build-bridge-bundle.mjs --install` builds the bundle and installs
> it into `ApplicationPlugins` for you.

### Option B - manual NETLOAD (debugging only)

1. Build or download `Civil3D.Bridge.dll` and its dependencies (Shared, Sdk, Domain,
   Tools) into one folder.
2. In Civil 3D run `NETLOAD`, select `Civil3D.Bridge.dll`.

The bundle in Option A is preferred because auto-load keeps the bridge running for
every session without user action.

---

## 3. Install the MCP Server

### From npm (recommended)

```bash
# Run on demand (no permanent install):
npx -y autodesk-mcp-server

# Or install globally:
npm install -g autodesk-mcp-server
```

### From the repository

```bash
npm install --prefix src/server/Autodesk.Mcp.Server
npm --prefix src/server/Autodesk.Mcp.Server run build
node src/server/Autodesk.Mcp.Server/dist/index.js --version
```

### Verify

```bash
autodesk-mcp-server --version
# 1.0.0-rc.1
autodesk-mcp-server --help
```

---

## 4. Configure your AI client

Every client launches the server as a stdio MCP server. Ready-made snippets:

- Claude Desktop -> `examples/clients/claude-desktop.json`
- VS Code -> `examples/clients/vscode-mcp.json`
- Cursor -> `examples/clients/cursor-mcp.json`
- Cline -> `examples/clients/cline-mcp.json`

The common shape is:

```json
{
  "mcpServers": {
    "autodesk-mcp": {
      "command": "npx",
      "args": ["-y", "autodesk-mcp-server"]
    }
  }
}
```

With the bridge installed and Civil 3D running, the client will discover every
bridge tool (`tools/list`) - typically 100+ tools for drawing, alignment, surface,
corridor, pipe network, quantity and engineering workflows.

---

## 5. Upgrade

### Bridge

1. Close Civil 3D.
2. Replace the old `%APPDATA%\Autodesk\ApplicationPlugins\Civil3D.Bridge.Bundle-<old>`
   folder with the new `Civil3D.Bridge.Bundle-<new>` folder.
3. (Recommended) delete the old folder to avoid stale auto-load entries.
4. Restart Civil 3D. The new descriptor advertises the new `bridgeVersion`.

### Server

```bash
npm install -g autodesk-mcp-server@latest   # global install
# or just relaunch with: npx -y autodesk-mcp-server@latest
```

Restart the MCP client to pick up the new binary.

---

## 6. Uninstall

### Bridge

1. Close Civil 3D.
2. Delete the bundle folder: `%APPDATA%\Autodesk\ApplicationPlugins\Civil3D.Bridge.Bundle-*`.
3. Optional cleanup:
   - `%LOCALAPPDATA%\AutodeskMcp\endpoints\*` (endpoint descriptors)
   - `%LOCALAPPDATA%\AutodeskMcp\logs\` (logs)

### Server

```bash
npm uninstall -g autodesk-mcp-server
```

Then remove the server entry from your client's MCP configuration (for Claude
Desktop: `claude_desktop_config.json`; VS Code: `.vscode/mcp.json`; Cursor/Cline:
their MCP settings).

---

## 7. What is installed where

| Item | Location |
| --- | --- |
| Bridge bundle | `%APPDATA%\Autodesk\ApplicationPlugins\Civil3D.Bridge.Bundle-<version>\` |
| Endpoint descriptors | `%LOCALAPPDATA%\AutodeskMcp\endpoints\` |
| Bridge logs | `%LOCALAPPDATA%\AutodeskMcp\logs\` |
| Server package | npm global `node_modules` / `npx` cache |
| Server config | wherever you place `server.config.json` (path passed via `-c`) |
