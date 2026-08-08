# Troubleshooting

Common problems, in order of likelihood.

---

## The client shows no tools / "No bridge is currently connected"

The server is running but has not connected to a bridge.

1. **Is Civil 3D running with the bridge loaded?**
   Check for an endpoint descriptor:
   ```bash
   dir %LOCALAPPDATA%\AutodeskMcp\endpoints
   ```
   No files -> the bridge is not loaded. Reinstall the bundle (see `Installation.md`)
   and restart Civil 3D. Look for a dialog or an error in the bridge log:
   ```bash
   type %LOCALAPPDATA%\AutodeskMcp\logs\civil3d-bridge-*.log
   ```
   - `Autodesk SDK not found at ...` - the bridge was built for a different AutoCAD
     folder; see *Bridge failed to initialize* below.
   - `Civil 3D Bridge initialized: ...` present -> the bridge is fine; continue.

2. **Is the server pointing at the right endpoint directory?**
   If you overrode `endpointsDir` / `AUTODESK_MCP_ENDPOINTS_DIR`, make sure it
   matches `%LOCALAPPDATA%\AutodeskMcp\endpoints`.

3. **Wait a few seconds.** Discovery polls every 3 s by default.

4. **Check server logs (stderr).** You should see `Bridge status changed to connected`.
   If you see `disconnected`, the pipe connection failed - see below.

---

## "Cannot connect to pipe" / bridge keeps disconnecting

- **Two Civil 3D sessions:** each instance owns its own pipe; the server picks the
  newest. If an old instance exited uncleanly, a stale descriptor may linger until
  the next poll cleans it (default 3 s).
- **Pipe name collision:** if you hand-set `pipeName` in `bridge.config.json`, ensure
  it is unique per machine.
- **Antivirus / policy:** some environments block named-pipe access for spawned
  processes. Add an exclusion for the Node.js binary and the Autodesk processes.

---

## Bridge failed to initialize (alert dialog in Civil 3D)

The plugin throws during `Initialize()` - details go to `%LOCALAPPDATA%\AutodeskMcp\logs\`.
The alert shows the full exception chain (root cause last), and failures are written to
`civil3d-bridge-*.log` even when the bridge could not load its own configuration (a
fallback logger with default settings is used in that case).

- **`Failed to load configuration from file '<bundle>\Contents\Configuration\bridge.config.json'`**
  The config file was missing, empty, or malformed when Civil 3D started - a bundle
  copied while the build was still writing is the usual cause (the file ends up empty or
  truncated). The inner exception shown in the dialog and the bridge log name the actual
  problem (for example a JSON parse error). Verify the file parses as JSON, rebuild and
  reinstall the bundle if needed, then restart Civil 3D.
- **`Autodesk SDK not found at 'C:\Program Files\Autodesk\AutoCAD 2025'`**
  The bridge was built against a different AutoCAD path than the installed one. Set
  the `AutodeskAcadDir` MSBuild property when building the bundle:
  ```bash
  node eng/scripts/build-bridge-bundle.mjs --msbuild "-p:AutodeskAcadDir=C:\Program Files\Autodesk\AutoCAD 2026"
  ```
- **Binding errors (`Could not load file or assembly ...`)** - a required dependency
  DLL is missing next to `Civil3D.Bridge.dll`. Rebuild the bundle (it copies all
  managed dependencies into `Contents/`).
- **Wrong .NET runtime** - the bridge requires the .NET 8 Desktop Runtime; Civil 3D
  2025/2026 ship it, but a stripped install may not. Install
  [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).

---

## Server exits immediately

- Run `autodesk-mcp-server --version`. If that fails, reinstall Node >= 20 or the
  package.
- `fatal startup error` on stderr with a config error -> fix the config file, or
  remove it (defaults apply).
- Some clients require the full path to the binary when `npx` is not on the client's
  PATH. Use `command: "node", args: ["C:\...\autodesk-mcp-server\dist\index.js"]`.

---

## Tools fail with `E_CONFIRMATION_REQUIRED`

Editing tools require confirmation. If your client does not support the elicitation
flow, retry the call with the `confirm: true` argument (the tool error message
includes this hint). Do not make it a habit for destructive operations.

---

## Tools fail with `E_TIMEOUT`

Large operations (corridor rebuild, surface comparison) can exceed the 30 s default
request timeout. Raise it for the server:

```json
{ "requestTimeoutMs": 120000 }
```

or the equivalent environment variable. Long-running tools declare their own
timeouts in the manifest; the server honors the larger of the two.

---

## Progress never arrives in the client

- Progress requires `supportsProgress` in the bridge config and the client passing a
  progress token (`_meta.progressToken`).
- Not all clients surface progress notifications; check the server stderr for
  progress-forwarding entries before assuming the pipe is at fault.

---

## Cancellation appears to do nothing

- Cancellation only applies to in-flight requests; if the tool already returned,
  nothing to cancel.
- The bridge honors `$/cancel` only for tools with `supportsCancellation`; some
  single-shot API calls cannot be interrupted mid-flight.

---

## Version mismatch errors

The handshake exchanges `protocolVersion`. Server and bridge must speak the same
**major** protocol version. Reinstall both from the same release. Bridge and server
versions do not have to be equal, but protocol majors must match.

---

## Stale artifacts from an older install

After upgrading, clean up leftovers:

```bash
# remove old bundle folders
rm -rf %APPDATA%\Autodesk\ApplicationPlugins\Civil3D.Bridge.Bundle-*
# remove stale endpoint descriptors
rm -f %LOCALAPPDATA%\AutodeskMcp\endpoints\*.json
```

Then restart Civil 3D and the client.

---

## Still stuck?

- Check the **server** structured logs (stderr) for the failing request's
  correlation id, and the **bridge** log for the same correlation id.
- Search or open an issue in the repository with both logs (redact drawing names if
  sensitive) and the exact tool call.

---

## Correlating failures across logs (Phase 9)

Every important operation can be traced with the following fields, all present in
server logs (pino, written to **stderr** - stdout stays clean for the protocol):

| Field | Log line |
| --- | --- |
| `correlationId` | `Tool <name> succeeded|failed ... (correlation <uuid>)` |
| `sessionId` | `Connected to <bridge> (session <id>, protocol <v>)` |
| tool name | every tool execution line |
| bridge instance / pipe | `Selected endpoint <name> (<product>, pipe <pipe>)` and `Connecting to bridge on pipe <pipe> (attempt <n>)` |
| execution duration | `Tool <name> completed in <ms> ms (correlation <uuid>)` |

Typical operator flows:

- **Bridge unavailable**: look for `Selected endpoint` absence or reconnect lines
  with increasing attempt numbers.
- **Pipe connection failure**: `Connecting to bridge on pipe ... (attempt N)`
  followed by `Bridge status changed to reconnecting`.
- **Protocol mismatch**: the handshake failure line names the refused version.
- **Tool timeout**: `Tool <name> failed with E_TOOL_TIMEOUT ...`.
- **Tool cancellation**: `Cancellation forwarded for correlation <uuid>`.
- **Autodesk transaction failure / invalid arguments**: `Tool <name> failed
  with <stable error code>: <safe message> (correlation <uuid>)`. Raw Autodesk
  stack traces stay in the bridge log only and never cross the protocol.

If a request seems stuck, first check that the correlation id appears in a
`completed` line; a missing line means the bridge never answered (reconnect or
bridge restart). See `docs/PRODUCTION-HARDENING.md` for the full hardening
report and recovery guarantees.
