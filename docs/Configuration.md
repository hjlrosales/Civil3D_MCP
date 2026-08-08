# Configuration

Both components are configured without touching source code - via JSON files and
environment variables. Precedence is always:

```
defaults  <  configuration file  <  environment variables
```

---

## 1. Server configuration (`Autodesk.MCP.Server`)

Pass a file with `-c <path>` / `--config <path>`, or set the `AUTODESK_MCP_CONFIG`
environment variable to the file path. A missing or malformed file is ignored
(defaults apply). Sample: `examples/config/server.config.json`.

| Option | Default | Meaning |
| --- | --- | --- |
| `logLevel` | `info` | pino level: `trace|debug|info|warn|error|fatal` |
| `endpointsDir` | `%LOCALAPPDATA%\AutodeskMcp\endpoints` | where bridge descriptors are scanned |
| `preferredProduct` | - | restrict selection to this product (e.g. `Civil3D`) |
| `preferredBridge` | - | prefer this logical bridge name |
| `reconnectDelayMs` | `1000` | base reconnect delay; doubles per failed attempt |
| `maxReconnectAttempts` | `10` | attempts before giving up (`0` = retry forever) |
| `requestTimeoutMs` | `30000` | per-request bridge timeout |
| `heartbeatIntervalMs` | `15000` | `health/ping` interval (`0` disables) |
| `endpointsPollIntervalMs` | `3000` | registry polling interval |
| `clientName` | `Autodesk.MCP.Server` | identity reported in the handshake |
| `clientVersion` | current release | version reported in the handshake |

### Environment variables

| Variable | Overrides |
| --- | --- |
| `AUTODESK_MCP_LOG_LEVEL` | `logLevel` |
| `AUTODESK_MCP_ENDPOINTS_DIR` | `endpointsDir` |
| `AUTODESK_MCP_PREFERRED_PRODUCT` | `preferredProduct` |
| `AUTODESK_MCP_PREFERRED_BRIDGE` | `preferredBridge` |
| `AUTODESK_MCP_RECONNECT_DELAY_MS` | `reconnectDelayMs` |
| `AUTODESK_MCP_MAX_RECONNECT_ATTEMPTS` | `maxReconnectAttempts` |
| `AUTODESK_MCP_REQUEST_TIMEOUT_MS` | `requestTimeoutMs` |
| `AUTODESK_MCP_HEARTBEAT_INTERVAL_MS` | `heartbeatIntervalMs` |
| `AUTODESK_MCP_ENDPOINTS_POLL_INTERVAL_MS` | `endpointsPollIntervalMs` |

See `examples/config/server.env.example` for a commented copy.

### Example

```bash
autodesk-mcp-server -c server.config.json
# with env overrides:
AUTODESK_MCP_LOG_LEVEL=debug AUTODESK_MCP_REQUEST_TIMEOUT_MS=60000 autodesk-mcp-server
```

---

## 2. Bridge configuration (`Civil3D.Bridge`)

The bridge reads `<bundle>/Contents/Configuration/bridge.config.json` next to the
assembly. All keys are optional; defaults are shown in
`examples/config/bridge.config.json`. The file uses a single `"bridge"` section:

```json
{
  "bridge": {
    "bridgeName": "Civil3D.Bridge",
    "product": "Civil3D",
    "productVersion": "2025",
    "bridgeVersion": "1.0.0-rc.1",
    "pipeName": "",
    "maxConcurrentConnections": 8,
    "supportedProducts": ["Civil3D"],
    "logDirectory": "",
    "supportsStreaming": false,
    "supportsProgress": false,
    "supportsCancellation": true,
    "supportsConfirmation": false,
    "supportsBatchRequests": false,
    "supportsParallelExecution": false
  }
}
```

| Key | Default | Meaning |
| --- | --- | --- |
| `bridgeName` | `Civil3D.Bridge` | logical name advertised in the descriptor |
| `product` / `productVersion` | `Civil3D` / `2025` | product identity in the descriptor |
| `bridgeVersion` | current release | semver advertised at handshake |
| `pipeName` | `autodesk-mcp-civil3d-<pid>` | pipe name (empty derives from PID) |
| `logDirectory` | `%LOCALAPPDATA%\AutodeskMcp\logs` | Serilog rolling-file folder |
| `maxConcurrentConnections` | `8` | simultaneous pipe sessions |
| `supportedProducts` | `["Civil3D"]` | products this bridge serves |
| `supports*` | see sample | capability flags sent to the server |

Edit the file and restart Civil 3D to apply.

---

## 3. Logging

- **Server:** structured JSON on **stderr** (stdout stays clean for MCP traffic).
  Redirect for file logging:
  ```bash
  autodesk-mcp-server 2>> server.log
  ```
  Sample lines: `examples/config/logging/server-stderr-sample.jsonl`.
- **Bridge:** Serilog rolling daily files under `%LOCALAPPDATA%\AutodeskMcp\logs\`
  (`civil3d-bridge-<date>.log`, 14 files retained). Sample:
  `examples/config/logging/bridge-log-sample.log`.

---

## 4. Multi-bridge / multi-instance

Bridges publish one endpoint descriptor each; the server selects the best live one:

- `preferredProduct` / `preferredBridge` pin a product or bridge name.
- Equal candidates resolve to the **most recently started** instance.
- Stale descriptors (dead PID) are ignored and cleaned up.

Examples: `examples/config/multi-bridge/` (multi-product selection, pinned bridge,
multiple Civil 3D instances).

---

## 5. Security notes

- All traffic is local: named pipes only, no network listeners. Pipe ACLs are
  restricted to the current user.
- Parameters are logged through redaction hooks; stack traces never cross the pipe.
- Editing tools require confirmation (the `confirm` argument on editing tools, or
  the client elicitation flow).
