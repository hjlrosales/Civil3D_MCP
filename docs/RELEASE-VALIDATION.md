# Release Validation

Acceptance checklist executed against the `v<version>` release artifacts before a
release is announced. Split into **automated** (pipeline + scripts) and
**manual/on-machine** (require Civil 3D and MCP clients).

---

## 1. Automated (must be green)

- [ ] `npm run quality` passes on a clean checkout (typecheck, lint, test, build,
      format, pack, bundle, E2E).
- [ ] .NET core suite: `dotnet test` on all non-Autodesk test projects passes.
- [ ] Bridge-dependent tests pass on the `civil3d` runner (or locally with the SDK).
- [ ] `node eng/scripts/sync-version.mjs` reports no drift; `git diff` shows only
      intended version changes.
- [ ] `node eng/scripts/build-bridge-bundle.mjs` produces the bundle folder + zip.
- [ ] `npm pack` produces `autodesk-mcp-server-<version>.tgz` with `dist/` + README.
- [ ] `node eng/scripts/release-notes.mjs` produces notes including the changelog
      entry and commit list.

## 2. Bridge bundle installs correctly

- [ ] Copy the bundle folder into `%APPDATA%\Autodesk\ApplicationPlugins\`.
- [ ] Restart Civil 3D; no error dialog.
- [ ] `%LOCALAPPDATA%\AutodeskMcp\endpoints\Civil3D-<pid>.json` appears with
      `bridgeVersion` == release version.
- [ ] Bridge log shows `Civil 3D Bridge initialized`.

## 3. Server installs via npm

- [ ] `npm install -g autodesk-mcp-server@<version>` succeeds on a clean machine.
- [ ] `autodesk-mcp-server --version` prints `<version>`.
- [ ] `npx -y autodesk-mcp-server` starts without error (no bridge needed).

## 4. Tool discovery in every supported client

For each client, start Civil 3D + bridge, then confirm the client lists the full
catalog (count == `tools/list` count from the bridge manifest):

- [ ] Claude Desktop discovers every tool.
- [ ] Cursor discovers every tool.
- [ ] VS Code MCP discovers every tool.
- [ ] Cline discovers every tool.

## 5. Runtime behaviours

- [ ] Read-only call returns data (e.g. `drawing_info`, `list_alignments`).
- [ ] Editing call returns `E_CONFIRMATION_REQUIRED`, then succeeds with `confirm: true`.
- [ ] Long-running tool reports progress (client shows progress notifications).
- [ ] Cancellation stops an in-flight long-running tool.
- [ ] Reconnect: close Civil 3D -> tool call fails with a bridge-unavailable error;
      restart Civil 3D -> tool call succeeds again without client restart.
- [ ] Multi-instance: two Civil 3D sessions; newest is selected; killing it fails
      over to the other session.
- [ ] Shutdown: closing the client does not leave orphan server/bridge processes.

## 6. Large-project smoke test

- [ ] Open a large drawing (many alignments/surfaces/corridors).
- [ ] `list_alignments`, `list_surfaces`, `corridor` and `calculate_cut_fill`
      complete without timeout (raise `requestTimeoutMs` if needed).
- [ ] `quantity_takeoff` and `export_landxml` produce correct output.

## 7. Sign-off

- [ ] All items checked; release announced and tagged as `v<version>`.

Record the date, versions and machine details next to the checklist when filing it
in an issue or release notes.
