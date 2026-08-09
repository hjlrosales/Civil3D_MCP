# 05 — Verification

"It should work" is not verification. Running a command and reading its output is.

All commands run from the repository root unless stated otherwise.

---

## 1. The one command that matters

```bash
npm run quality
```

Local mirror of CI: install → typecheck → lint → test → build → pack, plus the
.NET gate and version-drift check. **Green here is the bar for "done".**

Faster subsets:

```bash
npm run quality -- --node        # server only
npm run quality -- --dotnet      # .NET core (and bridge if the Autodesk SDK is present)
npm run quality -- --e2e         # end-to-end suite only
npm run quality:check            # version-drift only (seconds)
```

---

## 2. .NET

```bash
# Builds everywhere — no Civil 3D required
dotnet build AutodeskMcp.Core.slnx -c Release --nologo
dotnet test  AutodeskMcp.Core.slnx -c Release --nologo --no-build
dotnet format AutodeskMcp.Core.slnx --verify-no-changes --no-restore

# Full solution — requires Civil 3D / the Autodesk SDK on this machine
dotnet build AutodeskMcp.slnx -c Release --nologo
dotnet test  AutodeskMcp.slnx -c Release --nologo
```

Single project, when iterating:

```bash
dotnet test tests/Civil3D.Tools.Editing.Tests -c Release --nologo
dotnet test tests/Civil3D.Tools.Editing.Tests --filter "FullyQualifiedName~UpdatePipe"
```

Remember: warnings are errors under `src/`. A build that emits a warning fails.

---

## 3. TypeScript server

```bash
npm run typecheck:server
npm run lint:server
npm run test:server
npm run build:server
```

Or from `src/server/Autodesk.Mcp.Server/`: `npm run typecheck && npm run lint && npm test`.

---

## 4. End-to-end (spawns the real server process over stdio MCP)

```bash
npm run build:server
npm run test:e2e
```

Suites live in `e2e/suites/`: startup discovery, execution, reconnect,
progress/cancel, confirmation, multi-instance, shutdown. Run these for any
protocol, transport, discovery or lifecycle change.

---

## 5. Packaging and versions

```bash
npm run quality:check          # version drift across all versioned files
npm run sync:version           # regenerate versions from eng/version.json
npm run build:bridge           # assemble the Autodesk bundle
npm run validate:install       # fresh-install validation of the packed tarball
```

---

## 6. Live Civil 3D verification (manual — CI cannot do this)

Required for any change to bridge behaviour that unit tests cannot prove: real
transactions, part catalogs, document locking, application-context marshalling.

1. `dotnet build AutodeskMcp.slnx -c Release`
2. `npm run build:bridge`, then install the bundle into
   `%APPDATA%\Autodesk\ApplicationPlugins\` and restart Civil 3D.
3. Confirm the bridge registered:
   `%LOCALAPPDATA%\AutodeskMcp\endpoints\` contains a fresh
   `Civil3D-<pid>.json`.
4. Start the MCP client and confirm the tool catalog loads (a count of 0 means
   discovery failed — see `docs/Troubleshooting.md`).
5. Invoke the tool on a real drawing; check the returned envelope and the drawing.
6. Record what you ran and what you saw in the change report.

---

## 7. Levels of confidence — use these exact words

| Level | Means | Typical case |
| --- | --- | --- |
| **Verified** | Command run, output shown, passed | Server change, domain change |
| **Compiles only** | Builds + unit tests pass; never ran inside Civil 3D | Most bridge/tool changes |
| **Unverified** | Reasoning only; nothing executed | Docs, or a blocked environment |

State the level for **each** claim in the report. A change can be *verified* at
the unit level and *compiles only* at the integration level — say both.

---

## 8. What does not count as verification

- "The tests should pass."
- Pasting the code back and asserting it is correct.
- A build that succeeded before the last edit.
- Unit tests only, when the change is about transaction or threading behaviour —
  those are precisely what the in-memory harness cannot prove.
- Deleting, skipping or weakening a test until it goes green.
