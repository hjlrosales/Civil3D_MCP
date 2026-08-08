# Civil 3D Tool Development — The Standard (Phase 3B)

**Status:** Adopted
**Date:** 2026-08-07
**Applies to:** every tool assembly (`Civil3D.Tools.*`). This document is the contract for how to
build, register, test and document a Civil 3D tool.

---

## 1. Architecture at a glance

```
MCP Server ──named pipe──▶ Civil3D.Bridge ──▶ ToolDispatcher ──▶ Tool ──▶ Domain service ──▶ Autodesk API
                                     │                                   │
                                     └── ToolCatalog (discovery + manifest)┘
```

- **`Civil3D.Bridge`** is the host. It owns the plugin entry point, the DI composition root
  (`AddCivil3DBridge`), the dispatcher and the application-context marshaler. It contains **no tool
  implementations**.
- **`Civil3D.Tools.Abstractions`** is the shared, **Autodesk-free** base for every tool assembly:
  `Civil3DToolBase<TIn,TOut>`, `ICivil3DSession`, `ActiveDrawing`, `IDrawingStatisticsService`,
  `DrawingStatistics`. Because it never references Autodesk assemblies, it is trivially unit-testable
  and reused verbatim by future assemblies.
- **`Civil3D.Tools.Drawing`** is the first production tool assembly. It contains the tools
  (`drawing_info`, `drawing_summary`), their output DTOs, and the **real Autodesk implementations**
  of the service contracts.
- The SDK's `ToolCatalog` scans **every assembly loaded into the bridge process**; the bridge DI
  extension passes `AppDomain.CurrentDomain.GetAssemblies()`. Adding a tool assembly therefore
  requires **no per-tool registration** — the bridge references the assembly once (so it is loaded
  and deployed) and everything else is reflection.

Dependency direction (enforced, no cycles):

```
Civil3D.Bridge → Civil3D.Tools.Drawing → Civil3D.Tools.Abstractions → Autodesk.Mcp.Sdk → Autodesk.Mcp.Shared
```

---

## 2. How to build a new tool

A tool is **one C# class + one output DTO**. Everything else is infrastructure.

### Step 1 — Create the assembly (once per domain)

For a new domain, create `src/bridges/Civil3D.Tools.<Domain>/Civil3D.Tools.<Domain>.csproj` by
copying `Civil3D.Tools.Drawing.csproj` and changing:

- `RootNamespace` / `AssemblyName` / `Description`.
- The `ProjectReference` to `..\Civil3D.Tools.Abstractions\...` (and SDK + Shared).

The Autodesk reference block (`AcMgd`, `AcDbMgd`, `AcCoreMgd`, conditional `AeccDbMgd`), the
`MSBuildWarningsAsMessages` and the `EnsureAutodeskSdk` target stay identical.

### Step 2 — Declare the tool class

```csharp
[McpTool(
    "drawing_info",                                        // stable wire name (never rename)
    "Drawing Info",                                        // human label
    "Long markdown description...",                        // shown to the AI client
    Category = ToolCategory.Drawing,                       // functional category
    Permission = ToolPermission.ReadOnly,                  // ReadOnly | ModifyDrawing | Export | Administrative
    Risk = ToolRisk.Low,                                   // Low | Medium | High | Critical
    Version = "1.0.0",                                     // bump on any schema/behavior change
    SupportsCancellation = true,
    Tags = new[] { "drawing", "info" })]
public sealed class DrawingInfoTool : Civil3DToolBase<EmptyParameters, DrawingInfoDto>
{
    // Constructor injection only — never new-up dependencies inside the tool.
    public DrawingInfoTool(ICivil3DSession session, IEndpointInfoProvider bridgeInfo) : base(session) { ... }

    protected override Task<DrawingInfoDto> ExecuteToolCoreAsync(
        EmptyParameters input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ActiveDrawing drawing = RequireActiveDrawing(context);   // throws E_NO_ACTIVE_DOCUMENT
        ...
        return Task.FromResult(new DrawingInfoDto { ... });
    }
}
```

Rules enforced by the base class:

- `RequiresApplicationContext` is always `true` — every Autodesk call runs on the app thread via the
  dispatcher.
- The base performs standard exception handling: `BridgeException` and cancellation pass through
  unchanged; **any other exception is logged and mapped to `E_INTERNAL`**, so raw Autodesk
  exceptions never cross the pipe.
- The base logs execution time, drawing name, correlation/session ids and result size.

### Step 3 — Access the Autodesk API (never directly in the tool)

Tools **do not** call `Application.*` or open transactions themselves. They call an Autodesk-free
service contract whose real implementation lives in the same tool assembly:

| Concern | Contract (Abstractions) | Real implementation (Drawing) |
|---|---|---|
| Active document snapshot | `ICivil3DSession.GetActiveDrawing()` | `AutodeskCivil3DSession` |
| Drawing statistics | `IDrawingStatisticsService.GetStatistics(...)` | `AutodeskDrawingStatisticsService` |

The real implementations are the only code that touches Autodesk. They must:

- run on the application context (guaranteed by the dispatcher);
- read once per invocation into an immutable record, then return it;
- use **read-only transactions** only (`open → read → commit → dispose`, no editing);
- map every failure to a `BridgeException` (e.g. `E_TRANSACTION_FAILED`) — never leak Autodesk
  exception types.

> **Why not typed `Document`/`Database` members on the base?** Typed members would force the shared
> base to reference Autodesk assemblies and would make every tool test require a running Civil 3D.
> Keeping contracts Autodesk-free means tools, the base and all tests run headless; the Autodesk code
> is isolated in the service implementations — exactly the isolation the architecture mandates.
> Future editing domains add their own contracts (e.g. a transaction-capable alignment service) the
> same way.

### Step 4 — Create the output DTO

Immutable `record` with `init`-only properties, fully XML-documented. The DTO is the tool's wire
contract: the SDK generates its JSON Schema from the type at startup (no hand-written schemas), and
the dispatcher serializes it with `SharedJson.Options` (camelCase, nulls omitted).

### Step 5 — Validate the active document

Call `RequireActiveDrawing(context)` first. It returns the `ActiveDrawing` snapshot or throws
`BridgeException(E_NO_ACTIVE_DOCUMENT)` with correlation/session context. The dispatcher turns that
into the standard envelope; raw exceptions never escape.

---

## 3. How to register a new tool

There is **no per-tool registration**. In the bridge DI extension
(`BridgeServiceCollectionExtensions.AddCivil3DBridge`):

1. The catalog is constructed with `AppDomain.CurrentDomain.GetAssemblies()` — every loaded tool
   assembly is scanned by `ToolScanner` (classes implementing `ITool` decorated with `[McpTool]`).
2. Add a `ProjectReference` from `Civil3D.Bridge` to the new assembly so it is **loaded** (and
   deployed) — this also guarantees it appears in `GetAssemblies()` before the catalog is built.
3. Register the assembly's service implementations once:

```csharp
services.AddSingleton<ICivil3DSession, AutodeskCivil3DSession>();
services.AddSingleton<IDrawingStatisticsService, AutodeskDrawingStatisticsService>();
```

The tool classes themselves are instantiated lazily by the catalog through the DI container
(`ActivatorUtilities.CreateInstance`), so constructor injection just works.

---

## 4. How to return protocol responses

Tools return their **DTO** (`TOut`) from `ExecuteToolCoreAsync`. They never build envelopes:

- The dispatcher wraps the DTO in `ResponseEnvelope.Ok(data: <serialized DTO>)`.
- On failure the tool throws `BridgeException(ErrorCode, safeMessage, correlationId, sessionId)`;
  the dispatcher maps it to `ResponseEnvelope.Fail(...)` with the stable code.
- Successful envelope:
  `{ "success": true, "message": "", "executionTime": N, "errorCode": "E_UNKNOWN", "correlationId": "...", "sessionId": "...", "data": { ... } }`.

Error codes come from the shared frozen enum (`E_NO_ACTIVE_DOCUMENT`, `E_OBJECT_NOT_FOUND`,
`E_TRANSACTION_FAILED`, `E_TIMEOUT`, `E_CANCELLED`, `E_INTERNAL`, ...). Never invent new message
strings as error channels — pick the closest stable code.

---

## 5. Coding standards (every production tool)

- One responsibility per tool; no static mutable state; no captured mutable state on the (singleton)
  tool instance — pass `ToolExecutionContext` through parameters.
- Constructor injection only; never create dependencies inside the tool.
- Immutable DTOs (records) for all wire output; no anonymous objects, no dictionaries.
- All public API XML-documented (the projects compile with `TreatWarningsAsErrors` +
  `GenerateDocumentationFile`, so missing docs fail the build).
- Read the Autodesk API once per invocation; cache only values proven safe (the fingerprint enables
  future caching).
- Read-only tools use read-only transactions; editing tools (future phases) will take a
  `DocumentLock` and commit/rollback explicitly.

---

## 6. How to test a tool

New tools get a test project `tests/Civil3D.Tools.<Domain>.Tests` referencing the tool assembly,
the bridge and the SDK. It must run **headless** — never a live Civil 3D session.

- **Test doubles** (`TestDoubles.cs`): fake `ICivil3DSession`, fake `IDrawingStatisticsService`,
  fixed `IEndpointInfoProvider`, in-line `IApplicationContext`, canned `ActiveDrawing`/
  `DrawingStatistics` samples.
- **Unit tests:** DTO mapping (tool returns the expected DTO), no-active-document
  (`E_NO_ACTIVE_DOCUMENT`), error mapping (a throwing fake maps to the stable code and never exposes
  the inner message), serialization round-trips.
- **Manifest tests:** `ManifestGenerator.Generate(typeof(MyTool))` — name, category, permission,
  risk, version, schemas.
- **Discovery/registration tests:** `ToolScanner` finds the tool; `ToolCatalog` resolves and caches
  it; a full `AddCivil3DBridge` composition resolves it across loaded assemblies.
- **Integration test:** real `ToolCatalog` + real `ToolDispatcher` + real `JsonRpcRouter` +
  real `NamedPipeServerHost`, fakes for Autodesk services, a real pipe client. Verifies the full
  chain: discovery → manifest → routing → dispatcher → execution → protocol response.

Run:

```bash
dotnet test tests/Civil3D.Tools.Drawing.Tests/Civil3D.Tools.Drawing.Tests.csproj
```

---

## 7. Deliverable checklist (Phase 3B)

- [x] `Civil3D.Tools.Abstractions` — base class + contracts (no Autodesk refs).
- [x] `Civil3D.Tools.Drawing` — `drawing_info`, `drawing_summary`, DTOs, Autodesk service
      implementations.
- [x] Bridge wiring — project reference, service registration, discovery across loaded assemblies.
- [x] `tests/Civil3D.Tools.Drawing.Tests` — 19 tests incl. the end-to-end pipe integration test.
- [x] This document.
- [x] Full solution builds with `TreatWarningsAsErrors`; all tests pass.
