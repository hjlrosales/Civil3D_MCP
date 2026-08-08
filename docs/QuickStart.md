# QuickStart

Get from zero to "your AI agent is driving Civil 3D" in about five minutes.

---

## 1. Install the two pieces

**Bridge** (inside Civil 3D):

1. Download `Civil3D.Bridge.Bundle-<version>.zip`.
2. Extract and copy the `Civil3D.Bridge.Bundle-<version>` folder to
   `%APPDATA%\Autodesk\ApplicationPlugins\`.
3. Restart Civil 3D.

**Server** (npm):

```bash
npm install -g autodesk-mcp-server
autodesk-mcp-server --version
```

---

## 2. Verify the bridge is alive

```bash
dir %LOCALAPPDATA%\AutodeskMcp\endpoints
```

You should see a `Civil3D-<pid>.json` descriptor. (If not, check the bridge log at
`%LOCALAPPDATA%\AutodeskMcp\logs\`.)

---

## 3. Point your AI client at the server

Add one MCP server entry. Example for Claude Desktop
(`claude_desktop_config.json` - see `examples/clients/` for VS Code, Cursor, Cline):

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

Restart the client. The client's tool list should now include the full Civil 3D
tool catalog (100+ tools such as `list_alignments`, `list_surfaces`,
`calculate_cut_fill`, `quantity_takeoff`, `drawing_info`, ...).

---

## 4. Smoke test with a prompt

Ask something read-only first:

> What is in the current drawing? Summarize the layers, alignments and surfaces.

Then try an inspection workflow:

> List the alignments in this drawing. For each one show its length and station range.

Then a workflow (with confirmation, since it edits):

> Create a new alignment named "Relief Route" following the polyline on layer
> "ROAD-CL".

---

## 5. Where to go next

| Goal | Resource |
| --- | --- |
| Understand every option | `docs/Configuration.md` |
| Deeper example prompts | `examples/prompts/` |
| Full workflows | `examples/workflows/` |
| Something not working | `docs/Troubleshooting.md` |
| Client config snippets | `examples/clients/` |
| Real wire messages | `examples/json-rpc/` |
