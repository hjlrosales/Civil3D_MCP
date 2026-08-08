using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Autodesk.Mcp.Shared.Errors;
using Civil3D.Domain.Commands;
using Civil3D.Domain.Surfaces.Services;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Commands;
using Civil3D.Tools.Editing.Commands;
using Civil3D.Tools.Editing.Dtos;

namespace Civil3D.Tools.Editing.Tools;

/// <summary>
/// Tool <c>rename_surface</c>: renames a surface through the full command pipeline
/// (validation → confirmation → write transaction → commit/rollback → domain events → protocol
/// response). Inherits <see cref="CommandToolBase{TIn,TOut,TCommand,TResult}"/> so no orchestration
/// is duplicated.
/// </summary>
[McpTool(
    "rename_surface",
    "Rename Surface",
    "Renames a surface. Fails with E_OBJECT_NOT_FOUND when the id does not exist, " +
    "E_VALIDATION_FAILED for invalid or duplicate names, and E_TRANSACTION_FAILED on write failure.",
    Category = ToolCategory.Surfaces,
    Permission = ToolPermission.ModifyDrawing,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "surfaces", "edit", "rename" })]
public sealed class RenameSurfaceTool : CommandToolBase<RenameSurfaceRequest, RenameResult, RenameSurfaceCommand, RenameResult>
{
    private readonly ISurfaceService _surfaces;
    private readonly bool _requireConfirmation;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="dispatcher">The command dispatcher (full pipeline).</param>
    /// <param name="surfaces">The surface domain service (used to capture the current name).</param>
    /// <param name="confirmations">Confirmation gate; defaults to deny.</param>
    /// <param name="undo">Undo context; defaults to no-op.</param>
    /// <param name="requireConfirmation">When true, the rename requires explicit confirmation.</param>
    public RenameSurfaceTool(
        ICivil3DSession session,
        ICommandDispatcher dispatcher,
        ISurfaceService surfaces,
        IConfirmationGate? confirmations = null,
        IUndoContext? undo = null,
        bool requireConfirmation = false)
        : base(session, dispatcher, confirmations, undo)
    {
        _surfaces = surfaces ?? throw new ArgumentNullException(nameof(surfaces));
        _requireConfirmation = requireConfirmation;
    }

    /// <inheritdoc />
    protected override RenameSurfaceCommand CreateCommand(RenameSurfaceRequest input, ToolExecutionContext context)
    {
        var surface = _surfaces.GetById(input.ObjectId)
            ?? throw new BridgeException(
                ErrorCode.E_OBJECT_NOT_FOUND,
                $"No surface with id {input.ObjectId} was found.",
                context.CorrelationId,
                context.SessionId);

        return new RenameSurfaceCommand
        {
            ObjectId = input.ObjectId,
            PreviousName = surface.Name,
            NewName = input.NewName,
            RequiresConfirmation = _requireConfirmation,
        };
    }

    /// <inheritdoc />
    protected override RenameResult MapResult(RenameResult result) => result;
}
