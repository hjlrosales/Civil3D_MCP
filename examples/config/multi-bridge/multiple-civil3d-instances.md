# Multiple Civil 3D instances

Every bridge instance publishes a distinct endpoint descriptor:

```
%LOCALAPPDATA%\AutodeskMcp\endpoints\Civil3D-1234.json
%LOCALAPPDATA%\AutodeskMcp\endpoints\Civil3D-5678.json
```

Each descriptor carries its own `pipeName` (`autodesk-mcp-civil3d-<pid>`), so two
Civil 3D sessions never collide. The MCP server:

1. Polls the registry every `endpointsPollIntervalMs` (default 3000 ms).
2. Applies preferences: `preferredProduct`, then `preferredBridge`.
3. Among equal candidates, selects the **most recently started** descriptor.
4. Re-evaluates on every poll — when the selected instance exits, the next
   live instance is chosen automatically (or the server goes offline and
   reconnects later).

## Which instance will my AI client talk to?

- If both instances started at different times, the newer one wins.
- To pin an instance, run one server per instance and point each at a
  different endpoint directory (one `AutodeskMcp\endpoints` per user profile),
  or use `preferredProduct`/`preferredBridge` in a multi-product setup.

## Stale descriptors

Descriptors whose PID is no longer a live process are treated as stale and
removed on the next poll. A bridge that crashes leaves a stale descriptor
behind; the server ignores it and waits for a fresh one.
