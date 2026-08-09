# QuickStart (Plain-English Guide)

Get from zero to "my AI assistant is working inside Civil 3D" in about five
minutes.

This guide is the short version. If you get stuck on any step, the full
[Installation](Installation.md) guide explains everything in more detail.

---

## 1. Install the two pieces

You need two helper programs. Think of them this way:

- **The Bridge** — a plugin that lives inside Civil 3D. It's the AI's "eyes and
  hands" on your drawing.
- **The Server** — a small connecting program that sits between your AI assistant
  and the Bridge.

### Piece 1 — the Bridge (inside Civil 3D)

1. Download `Civil3D.Bridge.Bundle-<version>.zip` from the release page.
2. Right-click the file and choose **Extract All...**. A folder whose name looks
   like `Civil3D.Bridge.Bundle-1.0.0` appears.
3. Copy that whole folder into Civil 3D's plugins folder:
   - Open **File Explorer**, click into the address bar, type this exactly, and
     press Enter:
     ```
     %APPDATA%\Autodesk\ApplicationPlugins
     ```
     *(Don't worry about what `%APPDATA%` means — it's just Windows' shorthand
     for "your personal folders". Pasting it into the address bar always works.)*
4. Close Civil 3D completely, then open it again. The plugin loads itself — you
   don't have to click anything.

### Piece 2 — the Server (the connecting program)

Run this one line (npm installs the Server for you):

```
npm install -g autodesk-mcp-server
```

Check it worked:

```
autodesk-mcp-server --version
```

*(If you'd rather not install anything permanently, you can use the "download
when needed" way instead — see [Installation](Installation.md#3-install-the-server-the-connecting-program).)*

---

## 2. Make sure the Bridge is alive

The Bridge proves it's running by leaving a tiny "I'm ready" file on your
computer. To look at it:

1. Open **File Explorer**, click into the address bar, and paste:

   ```
   %LOCALAPPDATA%\AutodeskMcp\endpoints
   ```

2. Press Enter. You should see a small file whose name starts with `Civil3D-`,
   for example `Civil3D-12345.json`. (The number is just the ID Windows gave to
   this copy of Civil 3D — it changes every time, and that's normal.)

If you don't see the file, wait a few seconds after Civil 3D finishes opening and
look again. Still nothing? Check the log file in
`%LOCALAPPDATA%\AutodeskMcp\logs\`, or head to
[Troubleshooting](Troubleshooting.md).

---

## 3. Point your AI assistant at the Server

Now you tell your AI assistant "there's a Server you can use". This is done with
a tiny settings file — each assistant has its own.

For example, Claude Desktop uses a file called `claude_desktop_config.json`.
Add a small block like this (ready-made examples for VS Code, Cursor, and Cline
live in `examples/clients/`):

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

*(Plain English: `npx` means "download this tool for me and run it"; `-y` means
"yes, I really do want to run it".)*

**Then restart your AI assistant.** When it comes back, it should be able to see
all the Civil 3D tools — there are 100+, covering alignments, surfaces, corridors,
pipe networks, quantity takeoff, and more.

---

## 4. Try it out

Start with a question that only looks at the drawing (safe, no changes made):

> What is in the current drawing? Summarize the layers, alignments and surfaces.

Then try a read-only workflow:

> List the alignments in this drawing. For each one show its length and station range.

Then, when you're ready, try something that edits (the assistant will ask you to
confirm before it makes any changes):

> Create a new alignment named "Relief Route" following the polyline on layer "ROAD-CL".

---

## 5. Where to go next

| Goal | Resource |
| --- | --- |
| The full install walkthrough | `docs/Installation.md` |
| Understand every option | `docs/Configuration.md` |
| More example prompts | `examples/prompts/` |
| Full workflows | `examples/workflows/` |
| Something not working | `docs/Troubleshooting.md` |
| Client config snippets | `examples/clients/` |
| Real wire messages (for the curious) | `examples/json-rpc/` |
