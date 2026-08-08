# Performance Benchmarks

Measured metrics, where they live, and how to run them. The suites cover the
platform's hot paths and resilience characteristics; none of them require a running
Civil 3D session.

---

## Metric coverage

| Metric | .NET suite (`benchmarks/Autodesk.Mcp.Benchmarks`) | Node suite (`src/server/Autodesk.Mcp.Server/bench`) |
| --- | --- | --- |
| Startup | BridgeHost construction/start | real `node dist/index.js` spawn -> MCP `initialize` |
| Handshake | handshake DTO serialization | handshake over a real pipe + MCP initialize |
| Tool discovery | manifest generation from DTOs (NJsonSchema) | `tools/list` (MCP) and `tools/list` (bridge) with N tools |
| Large manifest loading | 200-tool manifest serialize/deserialize | 500-tool manifest load + diff + MCP registration |
| Workflow execution | multi-call execution sequence over the pipe | echo tool round-trips (100 calls) over stdio+pipe |
| Named pipe throughput | messages/sec + MB/s over a real pipe | execute round-trip latency distribution |
| Reconnect latency | reconnect timing in the SDK | bridge restart -> server re-discovery -> tools/list |
| Memory usage | GC heap delta for manifest generation | server process RSS/heap after load and executions |

---

## Running the .NET suite

```bash
npm run bench:dotnet
# or: dotnet run --project benchmarks/Autodesk.Mcp.Benchmarks -c Release
```

Self-contained harness (Stopwatch-based, no external packages):

- protocol envelope serialize/deserialize (`System.Text.Json`)
- handshake DTO round-trip
- manifest generation from a representative DTO set (NJsonSchema) + serialization
- large manifest (200 tools) load
- named-pipe round-trip latency + throughput (in-process SDK host + client)
- reconnect latency (connect, drop, reconnect)
- memory delta for manifest generation (`GC.GetTotalMemory`)

Prints a table to stdout and writes `benchmarks/Autodesk.Mcp.Benchmarks/results/`.

## Running the Node suite

```bash
npm run bench:server
# runs npm run build (server) then: vitest bench --config vitest.bench.config.ts
```

Uses vitest bench with the protocol-faithful `FakeBridge` and the real server
modules/process:

- startup: spawn `dist/index.js`, time to MCP `initialize` + first `tools/list`
- handshake + tool discovery against a real named pipe
- execute round-trips (echo tool, 100 iterations)
- large manifest (500 tools): load, diff, MCP registration time
- reconnect: kill bridge, restart with a new descriptor, time to reconnection
- memory: `process.memoryUsage()` sampled after load and after executions

---

## Interpreting results

- Pipe round-trips are typically sub-millisecond on local hardware; end-to-end
  MCP calls add Node stdio + JSON overhead (single-digit milliseconds).
- Manifest generation is dominated by NJsonSchema schema generation (tens of ms for
  100+ tools) and happens once per bridge connect, not per request.
- Reconnect is bounded by the endpoint poll interval (default 3000 ms) plus the
  backoff schedule; expect a few seconds for full recovery after a bridge restart.
- Memory is dominated by the manifest and ajv schema caches; expect tens of MB for
  the server process on large catalogs.

Run the suites on the same machine/configuration before comparing numbers.
