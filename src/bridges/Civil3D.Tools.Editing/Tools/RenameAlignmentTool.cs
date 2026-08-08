using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Domain.Alignments.Services;
using Civil3D.Domain.Commands;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Commands;
using Civil3D.Tools.Editing.Commands;
using Civil3D.Tools.Editing.Dtos;

namespace Civil3D.Tools.Editing.Tools;

/// <summary>
/// Tool <c>rename_alignment</c>: renames an alignment through the full command pipeline
/// (validation → confirmation → write transaction → commit/rollback → domain events → protocol
/// response). Inherits <see cref="CommandToolBase{TIn,TOut,TCommand,TResult}"/> so no orchestration
/// is duplicated.
/// </summary>
[McpTool(
    "rename_alignment",
    "Rename Alignment",
    "Renames an alignment. Fails with E_OBJECT_NOT_FOUND when the id does not exist, " +
    "E_VALIDATION_FAILED for invalid or duplicate names, and E_TRANSACTION_FAILED on write failure.",
    Category = ToolCategory.Alignments,
    Permission = ToolPermission.ModifyDrawing,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "alignments", "edit", "rename" })]
public sealed class RenameAlignmentTool : CommandToolBase<RenameAlignmentRequest, RenameResult, RenameAlignmentCommand, RenameResult>
{
    private readonly IAlignmentService _alignments;
    private readonly bool _requireConfirmation;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="dispatcher">The command dispatcher (full pipeline).</param>
    /// <param name="alignments">The alignment domain service (used to capture the current name).</param>
    /// <param name="confirmations">Confirmation gate; defaults to deny.</param>
    /// <param name="undo">Undo context; defaults to no-op.</param>
    /// <param name="requireConfirmation">When true, the rename requires explicit confirmation.</param>
    public RenameAlignmentTool(
        ICivil3DSession session,
        ICommandDispatcher dispatcher,
        IAlignmentService alignments,
        IConfirmationGate? confirmations = null,
        IUndoContext? undo = null,
        bool requireConfirmation = false)
        : base(session, dispatcher, confirmations, undo)
    {
        _alignments = alignments ?? throw new ArgumentNullException(nameof(alignments));
        _requireConfirmation = requireConfirmation;
    }

    /// <inheritdoc />
    protected override RenameAlignmentCommand CreateCommand(RenameAlignmentRequest input, ToolExecutionContext context)
    {
        var alignment = _alignments.GetById(input.ObjectId)
            ?? throw new Autodesk.Mcp.Shared.Errors.BridgeException(
                Autodesk.Mcp.Shared.Errors.ErrorCode.E_OBJECT_NOT_FOUND,
                $"No alignment with id {input.ObjectId} was found.",
                context.CorrelationId,
                context.SessionId);

        return new RenameAlignmentCommand
        {
            ObjectId = input.ObjectId,
            PreviousName = alignment.Name,
            NewName = input.NewName,
            RequiresConfirmation = _requireConfirmation,
        };
    }

    /// <inheritdoc />
    protected override RenameResult MapResult(RenameResult result) => result;
}
