using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Domain.Commands;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Commands;
using static Civil3D.Tools.Commands.Tests.TestCommands;

namespace Civil3D.Tools.Commands.Tests;

/// <summary>Input of the test command tools.</summary>
internal sealed record RecordLogInput
{
    public string? Label { get; init; }
}

/// <summary>Test-only command tool (ModifyDrawing) bound to <see cref="RecordLogCommand"/>.</summary>
[McpTool(
    "test_record_log",
    "Test Record Log",
    "Test-only command tool that exercises the Phase 5A command framework.",
    Category = ToolCategory.Objects,
    Permission = ToolPermission.ModifyDrawing,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true)]
internal sealed class RecordLogTool : CommandToolBase<RecordLogInput, RecordLogResult, RecordLogCommand, RecordLogResult>
{
    public RecordLogTool(
        ICivil3DSession session,
        ICommandDispatcher dispatcher,
        IConfirmationGate? confirmations = null,
        IUndoContext? undo = null)
        : base(session, dispatcher, confirmations, undo)
    {
    }

    protected override RecordLogCommand CreateCommand(RecordLogInput input, ToolExecutionContext context)
        => new() { Label = input.Label };

    protected override RecordLogResult MapResult(RecordLogResult result) => result;
}

/// <summary>Test-only command tool declared ReadOnly: the pipeline must reject its ModifyDrawing command.</summary>
[McpTool(
    "test_record_log_denied",
    "Test Record Log (Denied)",
    "Test-only tool declared ReadOnly whose command requires ModifyDrawing; exercises E_PERMISSION_DENIED.",
    Category = ToolCategory.Objects,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true)]
internal sealed class DeniedRecordLogTool : CommandToolBase<RecordLogInput, RecordLogResult, RecordLogCommand, RecordLogResult>
{
    public DeniedRecordLogTool(
        ICivil3DSession session,
        ICommandDispatcher dispatcher,
        IConfirmationGate? confirmations = null,
        IUndoContext? undo = null)
        : base(session, dispatcher, confirmations, undo)
    {
    }

    protected override RecordLogCommand CreateCommand(RecordLogInput input, ToolExecutionContext context)
        => new() { Label = input.Label };

    protected override RecordLogResult MapResult(RecordLogResult result) => result;
}

/// <summary>Test-only command tool bound to a confirmation-requiring command.</summary>
[McpTool(
    "test_destructive",
    "Test Destructive",
    "Test-only tool whose command requires explicit confirmation; exercises E_CONFIRMATION_REQUIRED.",
    Category = ToolCategory.Objects,
    Permission = ToolPermission.ModifyDrawing,
    Risk = ToolRisk.High,
    Version = "1.0.0",
    SupportsCancellation = true)]
internal sealed class DestructiveTool : CommandToolBase<RecordLogInput, RecordLogResult, DestructiveCommand, RecordLogResult>
{
    public DestructiveTool(
        ICivil3DSession session,
        ICommandDispatcher dispatcher,
        IConfirmationGate? confirmations = null,
        IUndoContext? undo = null)
        : base(session, dispatcher, confirmations, undo)
    {
    }

    protected override DestructiveCommand CreateCommand(RecordLogInput input, ToolExecutionContext context)
        => new() { Label = input.Label };

    protected override RecordLogResult MapResult(RecordLogResult result) => result;
}

/// <summary>Test-only command tool whose handler fails (exercises E_TRANSACTION_FAILED).</summary>
[McpTool(
    "test_failing",
    "Test Failing",
    "Test-only tool whose command handler fails; exercises the rollback + E_TRANSACTION_FAILED path.",
    Category = ToolCategory.Objects,
    Permission = ToolPermission.ModifyDrawing,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true)]
internal sealed class FailingTool : CommandToolBase<RecordLogInput, RecordLogResult, FailingCommand, RecordLogResult>
{
    public FailingTool(
        ICivil3DSession session,
        ICommandDispatcher dispatcher,
        IConfirmationGate? confirmations = null,
        IUndoContext? undo = null)
        : base(session, dispatcher, confirmations, undo)
    {
    }

    protected override FailingCommand CreateCommand(RecordLogInput input, ToolExecutionContext context) => new();

    protected override RecordLogResult MapResult(RecordLogResult result) => result;
}
