# Troubleshooting (Plain-English Guide)

Something not working? This page walks you through the most common problems, in
order of how likely you are to run into them. Each section tells you **how to
check** what's wrong and **what to do** about it.

A quick reminder of the two pieces you installed (see
[Installation](Installation.md)):

- **The Bridge** — the plugin inside Civil 3D. It's the "eyes and hands" on your
  drawing.
- **The Server** — the small connecting program between your AI assistant and the
  Bridge.

Most problems are one of: the Bridge isn't loaded, the Server can't reach the
Bridge, or the versions don't match. Let's work through them.

---

## The AI assistant shows no tools / "No bridge is currently connected"

The Server is running, but it can't find the Bridge. Check these in order:

**Step 1 — Is Civil 3D actually running with the Bridge loaded?**

The Bridge leaves a tiny "I'm ready" file when it starts. Look for it:

1. Open **File Explorer**, click into the address bar, and paste:
   ```
   %LOCALAPPDATA%\AutodeskMcp\endpoints
   ```
2. Press Enter.

- **You see a file whose name starts with `Civil3D-`** (for example
  `Civil3D-12345.json`)? Good — the Bridge is running. Jump to Step 3.
- **No files?** The Bridge isn't loaded. Reinstall the bundle and restart Civil 3D
  (see [Installation](Installation.md)). If it still doesn't load, look at the
  Bridge's diary — its log file:
  ```
  %LOCALAPPDATA%\AutodeskMcp\logs\civil3d-bridge-*.log
  ```
  Open the most recent file and look for:
  - A line starting with `Civil 3D Bridge initialized:` — great, the Bridge is
    fine, the problem is elsewhere.
  - `Autodesk SDK not found at ...` — the Bridge was built for a different
    AutoCAD folder; see *Civil 3D shows an error when starting* below.
  - Anything else that looks like an error — note the text; the sections below
    cover the common ones.

**Step 2 — Is the Server looking in the right place?**

Only applies if you changed the Server's settings. If you set a custom
`endpointsDir` (or the `AUTODESK_MCP_ENDPOINTS_DIR` environment variable), it
must point at the same folder shown above: `%LOCALAPPDATA%\AutodeskMcp\endpoints`.

**Step 3 — Give it a few seconds.**

The Server checks for the "I'm ready" file every few seconds by default. If you
just restarted Civil 3D, wait a moment and try again.

**Step 4 — Check the Server's own messages.**

The Server writes notes to the terminal window it runs in (in VS Code: the MCP
server's Output panel). It narrates every step, so the *last* line it managed to
print tells you where it stopped:

| Last line you see | What it means |
| --- | --- |
| `Searching for bridge endpoints in ...` | The Server started but found no "I'm ready" file. Go back to Step 1. |
| `Endpoint discovered: ...` | It found the Bridge and is about to connect. |
| `Connecting to bridge on pipe ...` | It is dialling. If nothing follows, the next section is for you. |
| `Handshake succeeded with ...` | Connected. The catalog is loading. |
| `Manifest loaded ... N tool(s) available` | Everything worked; `N` tools are ready. |
| `Advertised N tool(s) to the MCP client` | The Server told your assistant to refresh its tool list. |
| `No bridge is connected; 0 tools are available` | Civil 3D closed. The Server is fine and waiting — reopen Civil 3D and it reconnects by itself. |

**Step 5 — Are you running an old Server?**

The Server's first log line reports its version, for example
`Autodesk MCP Server 1.0.1 starting`. Versions before **1.0.1** had a bug where a
client that asked for the tool list before Civil 3D was discovered would keep
showing `0 tools` for the rest of the session, even though the Bridge was working
perfectly. If you see an older version, update:

```
npx -y autodesk-mcp-server@latest
```

or, for a permanent install, `npm install -g autodesk-mcp-server@latest`. Then
restart your assistant.

---

## VS Code says "Discovered 0 tools"

This is normal and temporary in exactly one case: **Civil 3D was not running when
VS Code started.** Start Civil 3D, wait a few seconds, and the tools appear on
their own — the Server watches for the Bridge continuously and tells VS Code as
soon as it finds it. You do not need to restart VS Code.

If Civil 3D *is* running and you still see `0 tools`, work through the steps in the
previous section — in particular Step 5, since a pre-1.0.1 Server shows this
permanently.

Things that should **never** be needed to fix this:

- Running `NETLOAD` in Civil 3D (the bundle loads itself).
- Restarting VS Code because you closed and reopened Civil 3D.
- Restarting Civil 3D because you closed and reopened VS Code.
- Deleting "I'm ready" files by hand.
- Opening Civil 3D and VS Code in a particular order.

---

## The Server keeps losing the connection / "Cannot connect to pipe"

The "pipe" is just the private two-way channel the Server uses to talk to the
Bridge. The usual causes:

- **Two copies of Civil 3D are open.** Each copy owns its own channel, and the
  Server talks to the most recently started one. If an older copy was closed
  abruptly, its "I'm ready" file can linger for a few seconds before the Server
  cleans it up. Close all but one copy of Civil 3D and try again.
- **A custom channel name was set.** If you hand-set `pipeName` in
  `bridge.config.json`, make sure it's a name no other machine or copy is using.
- **Antivirus or security software is blocking it.** Some security products block
  the private channel between programs. Try adding an exception for the Node.js
  program and the Autodesk programs (Civil 3D and the Bridge).

---

## Civil 3D shows an error when starting

The Bridge could not start up inside Civil 3D. The full reason is written to the
Bridge's log file (`%LOCALAPPDATA%\AutodeskMcp\logs\civil3d-bridge-*.log`) and
usually shown in a pop-up too. Common causes:

- **`Failed to load configuration from file '<bundle>\Contents\Configuration\bridge.config.json'`**
  The Bridge couldn't read its own settings file — usually because the file was
  missing, empty, or half-written when you copied the folder (this can happen if
  you copy the plugin while it's still being built). Fix: check the settings file
  is complete and well-formed, rebuild and reinstall the plugin (see
  [Installation](Installation.md)), then restart Civil 3D.

- **`Autodesk SDK not found at 'C:\Program Files\Autodesk\AutoCAD 2025'`**
  The plugin was built expecting AutoCAD in a different folder than where it's
  actually installed on your computer. If you build the plugin yourself, tell it
  where AutoCAD lives:
  ```
  node eng/scripts/build-bridge-bundle.mjs --msbuild "-p:AutodeskAcadDir=C:\Program Files\Autodesk\AutoCAD 2026"
  ```

- **`Could not load file or assembly ...`**
  A helper file that the Bridge needs is missing next to it. Rebuilding the plugin
  copies all the helper files into place — see [Installation](Installation.md).

- **".NET runtime" error.**
  The Bridge needs the .NET 8 Desktop Runtime. Civil 3D 2025/2026 normally bring
  it along, but a stripped-down install might not. Install it from
  [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0).

---

## The Server closes itself immediately

- Try `autodesk-mcp-server --version` in a terminal. If that command fails,
  Node.js (version 20 or newer) or the Server package may not be installed
  properly — reinstall both and try again.
- If the Server prints `fatal startup error` followed by a settings problem, its
  settings file is the culprit. Fix the file, or delete it so the Server uses its
  built-in defaults.
- Some AI assistants can't find the `npx` program. If so, point the assistant
  straight at the Server file with the `node` command:
  ```json
  {
    "mcpServers": {
      "autodesk-mcp": {
        "command": "node",
        "args": ["C:\\...\\autodesk-mcp-server\\dist\\index.js"]
      }
    }
  }
  ```

---

## Tools say `E_CONFIRMATION_REQUIRED`

Editing tools ask for your permission before changing the drawing. Your AI
assistant should ask you and pass your answer along. If your assistant doesn't do
this automatically, you can add `"confirm": true` to the tool call yourself (the
error message tells you this). Just don't get into the habit of always approving
destructive actions without reading what they do.

---

## Tools say `E_TIMEOUT` ("took too long")

Big jobs (rebuilding a corridor, comparing surfaces) can take longer than the
default 30-second limit. Give the Server more time by putting this in its
settings:

```json
{ "requestTimeoutMs": 120000 }
```

(Or use the matching environment variable.) Some tools declare their own, longer
timeouts — the Server uses whichever is longer.

---

## Progress updates never show up in the assistant

- Progress messages only work if the Bridge's settings have `supportsProgress`
  turned on **and** your AI assistant asks for them. Not every assistant does.
- Before blaming the connection, check the Server's terminal output for
  progress-related lines.

---

## Cancel doesn't seem to do anything

- Cancelling only works on a request that's still running. If the tool already
  finished, there's nothing to cancel.
- The Bridge only honours cancels for tools that support cancellation, and some
  one-shot drawing operations simply can't be interrupted halfway through.

---

## Version mismatch errors

When the Server and the Bridge first meet, they compare version numbers. They must
agree on the **major** protocol version. If they don't, reinstall **both** from
the same release (the Bridge inside Civil 3D and the Server package). They don't
have to be the exact same version — just the same major version.

---

## Leftover junk from an older version

After upgrading, old files can linger and cause confusion. Clean them up (in a
terminal):

```
# remove old plugin folders
rm -rf %APPDATA%\Autodesk\ApplicationPlugins\Civil3D.Bridge.Bundle-*
# remove stale "I'm ready" files
rm -f %LOCALAPPDATA%\AutodeskMcp\endpoints\*.json
```

Then restart Civil 3D and your AI assistant.

---

## Still stuck? How to get help (for advanced users)

When you report a problem, the two log files tell the whole story:

- **Server log** — the terminal output where the Server runs (it prints notes to
  the screen; the message stream it talks to the AI over stays clean).
- **Bridge log** — `%LOCALAPPDATA%\AutodeskMcp\logs\civil3d-bridge-*.log`.

Every request gets a special **correlation id** — a long string of letters and
numbers — that appears in **both** logs. Searching for the same id in both files
shows exactly what happened to one request from start to finish. Useful things to
look for:

| You want to know... | Look for... |
| --- | --- |
| Did the Bridge ever answer? | The correlation id in a "completed" line. If it's missing, the Bridge never responded. |
| Which Bridge copy was used? | `Selected endpoint <name> (<product>, pipe <pipe>)` |
| A timeout | `Tool <name> failed with E_TOOL_TIMEOUT ...` |
| A cancelled request | `Cancellation forwarded for correlation <uuid>` |
| A refused connection | `Connecting to bridge on pipe ... (attempt N)` with a rising number |
| A failed tool | `Tool <name> failed with <error code>: <message> (correlation <uuid>)` |

When opening an issue in the repository, include **both** logs and the exact tool
call you used. If your drawing has sensitive names, you can remove them first —
the request and the error codes are what matter.
