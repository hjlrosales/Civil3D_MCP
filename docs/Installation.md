# Installation (Plain-English Guide)

This guide shows you how to set up the Autodesk MCP Platform — a set of tools that
let your AI assistant (Claude, Cursor, VS Code, and so on) read and edit your
Civil 3D drawings. You don't need to write any code. Everything comes ready-made.

## What you are actually installing

Two small helper programs:

1. **The Bridge** — a plugin that lives *inside* Civil 3D. It is the "eyes and
   hands": it looks at your drawing and makes changes for the AI when asked.
2. **The Server** — a small connecting program that runs on your computer and acts
   as the phone line between your AI assistant and the Bridge.

You install both once. After that, every time you open Civil 3D, the Bridge starts
by itself.

---

## 1. What you need before you start

| What you need | Why | More info |
| --- | --- | --- |
| Windows 10 or 11 | The tools run on Windows only. | |
| Civil 3D 2025 or 2026 (the full program) | The Bridge plugs into Civil 3D itself. A free viewer or "Object Enabler" is **not** enough. | |
| Node.js | A free, safe program that runs the Server. | Download from [nodejs.org](https://nodejs.org) — pick the "LTS" version. |
| An AI assistant | Claude Desktop, Cursor, VS Code, Cline, or any MCP-compatible client. | You probably already have one. |

If you have all four, you're ready.

---

## 2. Install the Bridge (the Civil 3D plugin)

### Option A — the easy way (recommended)

**Step 1: Download the plugin.** Go to the project's releases page and download the
file named `Civil3D.Bridge.Bundle-<version>.zip`.

**Step 2: Unzip it.** Right-click the downloaded `.zip` file and choose
**Extract All...**. This creates a folder whose name looks like
`Civil3D.Bridge.Bundle-1.0.0`.

**Step 3: Copy the folder into Civil 3D's plugins folder.** Civil 3D automatically
loads anything placed in this special folder.

- The folder is inside a *hidden* Windows folder called **AppData**. Don't worry —
  you don't have to hunt for it. Here is the trick:
  1. Open **File Explorer** (the folder window).
  2. Click into the address bar at the top.
  3. Type (or paste) this exactly and press Enter:

     ```
     %APPDATA%\Autodesk\ApplicationPlugins
     ```

     *(Tip: `%APPDATA%` is just Windows' shorthand for "your personal AppData
     folder". Windows understands it if you type it into the address bar.)*

  4. Copy the whole `Civil3D.Bridge.Bundle-<version>` folder you unzipped into
     this window. When you look inside it, you should see a file called
     `PackageContents.xml` right there at the top.

**Step 4: Restart Civil 3D.** Close it completely, then open it again. The plugin
loads automatically — you don't need to click anything.

**Step 5: Make sure it worked.** Open File Explorer, click the address bar, and
type:

```
%LOCALAPPDATA%\AutodeskMcp\endpoints
```

You should see a small file whose name starts with `Civil3D-`, for example
`Civil3D-12345.json`. That file is the Bridge's "I'm ready" signal. (The number is
just the ID Windows gave to this copy of Civil 3D — it changes every time, and
that's normal.)

If you don't see it, wait a few seconds after Civil 3D finishes opening and look
again. If it still isn't there, check the log file (see
[Troubleshooting](Troubleshooting.md)).

> **Already have the source code?** You can build and install the plugin
> automatically with one command instead of doing Steps 1–3 by hand:
>
> ```
> node eng/scripts/build-bridge-bundle.mjs --install
> ```

### Option B — the manual way (only for experts / debugging)

This is the "load the file by hand" method. Use it only if you're testing a build
or the automatic way isn't working.

1. Get the plugin files (`Civil3D.Bridge.dll` plus its helper files) into one
   folder — either by downloading or building them.
2. In Civil 3D, type `NETLOAD` in the command line and press Enter.
3. Pick `Civil3D.Bridge.dll` and click Open.

The automatic way (Option A) is better for everyday use because the plugin starts
by itself every time — you never have to think about it.

---

## 3. Install the Server (the connecting program)

The Server is installed with `npm`, the package manager that comes with Node.js.
You only need to use one of the two ways below.

### Easiest: let it download itself when needed (recommended)

You don't install anything permanently. Instead, your AI assistant runs this one
line whenever it starts:

```
npx -y autodesk-mcp-server
```

*(Plain English: `npx` is a small program that says "download this tool for me and
run it". `-y` means "yes, I really do want to run it". The first run downloads the
Server; after that it's fast.)*

You won't type this command yourself — you'll put it in your AI assistant's
settings in the next section, and the assistant runs it for you.

### Alternative: install it permanently

If you prefer, install the Server on your computer once:

```
npm install -g autodesk-mcp-server
```

`npm` is the program that installs things; `-g` means "make it available
everywhere on this computer". You can check it worked with:

```
autodesk-mcp-server --version
```

### For developers: run it from the source code

```
npm install --prefix src/server/Autodesk.Mcp.Server
npm --prefix src/server/Autodesk.Mcp.Server run build
node src/server/Autodesk.Mcp.Server/dist/index.js --version
```

---

## 4. Connect your AI assistant

The last step is telling your AI assistant "hey, there's a Server you can use".
This is done with a tiny settings file — each assistant has its own.

Ready-made examples live in the `examples/clients/` folder:

- **Claude Desktop** → `examples/clients/claude-desktop.json`
- **VS Code** → `examples/clients/vscode-mcp.json`
- **Cursor** → `examples/clients/cursor-mcp.json`
- **Cline** → `examples/clients/cline-mcp.json`

In every case, you're adding a small block that looks like this:

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

The exact place to paste it differs by assistant. For example, Claude Desktop uses
a file called `claude_desktop_config.json`, VS Code uses `.vscode/mcp.json`, and
Cursor and Cline have their own settings screens. The example files show the right
spot for each.

**Then restart your AI assistant.** When it comes back, it should be able to see
all the Civil 3D tools (there are 100+ — for drawing, alignments, surfaces,
corridors, pipe networks, quantities, and more). You can confirm by asking it
something simple, like: *"What is in the current drawing?"*

---

## 5. Update

### Bridge (the Civil 3D plugin)

1. Close Civil 3D completely.
2. Download the new plugin folder (same steps as [section 2](#2-install-the-bridge-the-civil-3d-plugin)).
3. Replace the old folder: delete the old `Civil3D.Bridge.Bundle-<old version>`
   folder in `%APPDATA%\Autodesk\ApplicationPlugins`, then copy the new one in its
   place. (Deleting the old one avoids confusion between two versions.)
4. Open Civil 3D again. It loads the new version automatically.

### Server

- Installed permanently? Run:
  ```
  npm install -g autodesk-mcp-server@latest
  ```
- Using the "download when needed" way? Just use `npx -y autodesk-mcp-server@latest`
  the next time — you'll get the newest version automatically.

Either way, restart your AI assistant afterwards so it picks up the new version.

---

## 6. Uninstall

### Bridge (the Civil 3D plugin)

1. Close Civil 3D.
2. Delete the plugin folder: in File Explorer, go to `%APPDATA%\Autodesk\ApplicationPlugins`
   and delete every folder whose name starts with `Civil3D.Bridge.Bundle-`.
3. (Optional tidy-up) Delete `%LOCALAPPDATA%\AutodeskMcp` — that's just the
   "I'm ready" files and the log files, and Civil 3D will recreate them if you
   ever reinstall the plugin.

### Server

1. If you installed it permanently, remove it:
   ```
   npm uninstall -g autodesk-mcp-server
   ```
2. Remove the `autodesk-mcp` block you added to your AI assistant's settings in
   [section 4](#4-connect-your-ai-assistant).

---

## 7. Where everything lives (handy reference)

| What | Where |
| --- | --- |
| The Bridge plugin | `%APPDATA%\Autodesk\ApplicationPlugins\Civil3D.Bridge.Bundle-<version>\` |
| "I'm ready" files | `%LOCALAPPDATA%\AutodeskMcp\endpoints\` |
| Log files (for troubleshooting) | `%LOCALAPPDATA%\AutodeskMcp\logs\` |
| The Server | Installed by npm (or in the `npx` download cache) |
| Server settings file | Wherever you put `server.config.json` (you tell it the path with `-c`) |

**One last tip:** the `%...%` shortcuts in the table might look cryptic, but they
always work in File Explorer's address bar — just paste them in and press Enter.
They mean "go to this special folder on this computer, wherever it happens to be".
