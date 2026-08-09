# 04 — Patterns

Concrete, repo-specific shapes to copy. When in doubt, open the real file cited
under each pattern and follow it exactly — these snippets are abridged.

---

## P1 — A read-only tool

**Reference:** `src/bridges/Civil3D.Tools.Drawing/Tools/DrawingInfoTool.cs`
**Full contract:** `docs/TOOL-DEVELOPMENT.md`

Anatomy: one tool class + one output DTO. Nothing else.

```csharp
[McpTool(
    "drawing_info",                       // stable wire name — NEVER renamed after shipping
    "Drawing Info",                       // human label
    "Full description shown to the AI client. Say what it returns, what the "
    + "inputs mean, and which error codes it can fail with.",
    Category    = ToolCategory.Drawing,
    Permission  = ToolPermission.ReadOnly, // ReadOnly | ModifyDrawing | Export | Administrative
    Risk        = ToolRisk.Low,            // Low | Medium | High | Critical
    Version     = "1.0.0",                 // bump on ANY schema or behaviour change
    SupportsCancellation = true,
    Tags = new[] { "drawing", "info" })]
public sealed class DrawingInfoTool : Civil3DToolBase<EmptyParameters, DrawingInfoDto>
{
    private readonly IDrawingStatisticsService _statistics;

    /// <summary>Creates the tool.</summary>
    public DrawingInfoTool(ICivil3DSession session, IDrawingStatisticsService statistics)
        : base(session) => _statistics = statistics;   // constructor injection only

    /// <inheritdoc />
    protected override Task<DrawingInfoDto> ExecuteToolCoreAsync(
        EmptyParameters input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ActiveDrawing drawing = RequireActiveDrawing(context);   // throws E_NO_ACTIVE_DOCUMENT
        return Task.FromResult(new DrawingInfoDto { /* … */ });
    }
}
```

Checklist for a read tool:

- [ ] `RequireActiveDrawing(context)` is the first thing after the cancellation check.
- [ ] No `Application.*`, no `Transaction`, no Autodesk type in the tool class.
- [ ] Autodesk work happens in a service implementation in the same assembly, on
      a read-only transaction, returning an immutable snapshot.
- [ ] Description names the error codes it can produce.

---

## P2 — The output DTO

```csharp
/// <summary>Result of <c>drawing_info</c>.</summary>
public sealed record DrawingInfoDto
{
    /// <summary>File name of the active drawing, without path.</summary>
    public required string DrawingName { get; init; }

    /// <summary>Number of alignments in the drawing.</summary>
    public int AlignmentCount { get; init; }
}
```

- Immutable `record`, `init`-only, XML doc on **every** property.
- The SDK generates the JSON Schema from this type at startup. Do not write a
  schema by hand; there is nowhere to put it.
- Serialized with `SharedJson.Options` (camelCase, nulls omitted). Property names
  on the wire are camelCase — account for that when writing client-facing docs.

---

## P3 — An editing (write) tool

**Reference:** `src/bridges/Civil3D.Tools.Editing/Tools/UpdatePipeTool.cs`
**Framework:** `docs/COMMAND-FRAMEWORK.md`

Write tools do **not** subclass `Civil3DToolBase` directly — they subclass
`CommandToolBase<TRequest, TResponse, TCommand, TResult>` (defined in
`src/bridges/Civil3D.Tools.Commands/CommandToolBase.cs`) and delegate to the
command pipeline (validation → confirmation → write transaction →
commit/rollback → domain events → response).

```csharp
public sealed class UpdatePipeTool
    : CommandToolBase<UpdatePipeRequest, UpdatePipeResult, UpdatePipeCommand, UpdatePipeResult>
{
    public UpdatePipeTool(
        ICivil3DSession session,
        ICommandDispatcher dispatcher,
        IConfirmationGate? confirmations = null,   // defaults to deny
        IUndoContext? undo = null,                 // defaults to no-op
        bool requireConfirmation = false)
        : base(session, dispatcher, confirmations, undo) { … }

    protected override UpdatePipeCommand CreateCommand(UpdatePipeRequest input, ToolExecutionContext context)
        => new() { PipeId = input.PipeId, /* … */ RequiresConfirmation = _requireConfirmation };

    protected override UpdatePipeResult MapResult(UpdatePipeResult result) => result;
}
```

The five files a new editing operation needs, all in `Civil3D.Tools.Editing/`:

| File | Folder | Purpose |
| --- | --- | --- |
| `<Op>Request.cs` | `Dtos/` | Wire input DTO |
| `<Op>Command.cs` | `Commands/` | The command object |
| `<Op>CommandHandler.cs` | `Commands/` | Executes inside the write transaction |
| `<Op>CommandValidator.cs` | `Validators/` | Structural validation → `E_VALIDATION_FAILED` |
| `<Op>Tool.cs` | `Tools/` | The thin `[McpTool]` wrapper above |

Checklist for a write tool:

- [ ] `Permission = ToolPermission.ModifyDrawing`, `Risk` at least `Medium`.
- [ ] Validator rejects "no change requested" and out-of-range values.
- [ ] Unknown id → `E_OBJECT_NOT_FOUND`; Civil 3D rejection → `E_TRANSACTION_FAILED`.
- [ ] Result reports what actually changed (e.g. `ChangesApplied`), so the client
      can tell partial success from full success.
- [ ] Undo is registered through `IUndoContext`.

---

## P4 — Tool tests (xUnit, headless)

**Reference:** `tests/Civil3D.Tools.Editing.Tests/UpdatePipeToolTests.cs`

Drive the tool the way the dispatcher does — serialize the request to a
`JsonElement` with `SharedJson.Options` and call `ExecuteAsync`. That exercises
schema binding, not just your method.

```csharp
private static async Task<UpdatePipeResult> UpdatePipeAsync(Container c, UpdatePipeRequest request)
{
    var context = new ToolExecutionContext
    {
        ToolName = "update_pipe", CorrelationId = "c-2", SessionId = "s-2",
    };
    var parameters = JsonSerializer.SerializeToElement(request, SharedJson.Options);
    return (UpdatePipeResult)(await c.UpdatePipeTool.ExecuteAsync(context, parameters))!;
}

[Fact]
public async Task UpdatePipe_ElevationOnly_SetsBothEndsAndRaisesEvent()
{
    Container c = Create();                                   // EditingTestHarness
    CreatePipeResult created = await CreatePipeAsync(c, HdpeRequest());

    UpdatePipeResult result = await UpdatePipeAsync(c, new UpdatePipeRequest
    {
        PipeId = created.PipeId, ElevationMeters = 98.25,
    });

    Assert.True(result.Success);
    Assert.Equal(new[] { "elevation" }, result.ChangesApplied);
}
```

Conventions:

- Test name: `Method_Scenario_ExpectedOutcome`.
- Use the assembly's harness (`EditingTestHarness.Create()`, `InMemoryDrawing`)
  rather than mocking the Autodesk API.
- Assert on the DTO and the raised domain events.
- Failure cases get their own `[Fact]` each — validation failure, unknown id,
  confirmation denied, cancellation.
- Class-level XML doc stating what the suite covers (match the existing files).

---

## P5 — Server (TypeScript) change

**Reference:** `src/server/Autodesk.Mcp.Server/src/` + `test/`

- Find the owning module first: transport framing → `transport/`, catalog and
  connections → `manager.ts`, MCP surface → `mcp/`, discovery → `discovery/`.
- Every module has a matching `test/<name>.test.ts`. Add cases there.
- Never introduce a Civil 3D concept. If a fix needs one, the fix belongs in the
  bridge.
- Reconnect, cancellation and multi-bridge behaviour are already handled — read
  `manager.ts` and `bridgeConnection.ts` before adding retry logic of your own.

---

## P6 — Error handling

```csharp
// ErrorCode is an enum in Autodesk.Mcp.Shared/Errors/ErrorCode.cs
throw new BridgeException(ErrorCode.E_OBJECT_NOT_FOUND, $"Pipe '{id}' does not exist.");
```

- Pick the existing enum member that fits; adding a new one is a contract change.
- Message states the fact and the offending value, not the remedy.
- Never catch-and-swallow. Never wrap an Autodesk exception and rethrow it raw —
  let the base class map unexpected exceptions to `E_INTERNAL`.
- Common codes: `E_NO_ACTIVE_DOCUMENT`, `E_VALIDATION_FAILED`,
  `E_OBJECT_NOT_FOUND`, `E_TRANSACTION_FAILED`, `E_INTERNAL`.

---

## P7 — Adding a whole new tool assembly (rare)

Only when a genuinely new domain appears.

1. Copy `Civil3D.Tools.Drawing.csproj` → `Civil3D.Tools.<Domain>.csproj`; change
   `RootNamespace`, `AssemblyName`, `Description`.
2. Keep the Autodesk reference block, `MSBuildWarningsAsMessages` and the
   `EnsureAutodeskSdk` target byte-identical.
3. Add a single `ProjectReference` from `Civil3D.Bridge` so the assembly is
   loaded and deployed. That is the *only* registration — reflection does the rest.
4. Add `tests/Civil3D.Tools.<Domain>.Tests` mirroring an existing test project.
5. Add both projects to `AutodeskMcp.slnx` (and the test project to
   `AutodeskMcp.Core.slnx` if it builds without the Autodesk SDK).
