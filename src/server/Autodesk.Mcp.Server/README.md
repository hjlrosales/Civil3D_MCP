# autodesk-mcp-server

Model Context Protocol (MCP) server that exposes a running **Civil3D.Bridge** tool
catalog to any MCP client (Claude Desktop, Cursor, VS Code, Cline, ...).

The server is product-agnostic: it discovers bridge instances from the Autodesk MCP
endpoint registry (`%LOCALAPPDATA%\AutodeskMcp\endpoints`), handshakes over a local
Windows named pipe, loads the bridge's tool manifest dynamically, and registers every
tool as an MCP tool. No tool is hardcoded.

## Requirements

- Node.js **>= 20** (Windows)
- A running Autodesk product with the bridge loaded:
  - `Civil3D.Bridge` inside Autodesk Civil 3D 2025/2026 (see the platform
    [installation guide](https://github.com/your-org/autodesk-mcp-platform/blob/main/docs/Installation.md))

## Usage

```bash
npx autodesk-mcp-server            # or: npx -y autodesk-mcp-server
```

Or install globally / locally:

```bash
npm install -g autodesk-mcp-server
autodesk-mcp-server --config server.config.json
```

CLI:

```
autodesk-mcp-server [options]
  -c, --config <path>   Path to a JSON configuration file (or $AUTODESK_MCP_CONFIG)
  -V, --version         Print the server version and exit
  -h, --help            Print help and exit
```

## Configuration

Precedence: **defaults -> configuration file -> environment variables**. See
[Configuration.md](https://github.com/your-org/autodesk-mcp-platform/blob/main/docs/Configuration.md)
for the full option reference and `examples/config/server.config.json` for a sample.

All logging is structured JSON on **stderr**; stdout is reserved for MCP traffic.

## Client setup

Point your MCP client at the server binary. Ready-made configurations for Claude
Desktop, VS Code, Cursor and Cline live in
[`examples/clients`](https://github.com/your-org/autodesk-mcp-platform/tree/main/examples/clients).

## License

MIT
