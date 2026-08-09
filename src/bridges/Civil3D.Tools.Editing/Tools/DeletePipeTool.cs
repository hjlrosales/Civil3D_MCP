using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Domain.Commands;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Commands;
using Civil3D.Tools.Editing.Commands;
using Civil3D.Tools.Editing.Dtos;

namespace Civil3D.Tools.Editing.Tools;

/// <summary>
/// Tool <c>delete_pipe</c>: removes an existing pipe from its pipe network by its stable numeric
/// id through the full command pipeline (validation → confirmation → write transaction →
/// commit/rollback → domain events → protocol response), then best-effort saves the drawing so
/// the deletion persists. Fails with E_OBJECT_NOT_FOUND when the pipe id does not exist and
/// E_VALIDATION_FAILED when the id is not positive.
/// </summary>
[McpTool(
    "delete_pipe",
    "Delete Pipe",
    "Deletes an existing pipe from its pipe network by the stable pipeId returned by create_pipe " +
    "or list_pipe_networks. The pipe is removed inside a write transaction and the drawing is " +
    "then saved (best-effort) so the deletion persists across restarts. Fails with " +
    "E_OBJECT_NOT_FOUND when the pipe does not exist and E_VALIDATION_FAILED when the id is not " +
    "a positive number.",
    Category = ToolCategory.PipeNetworks,
    Permission = ToolPermission.ModifyDrawing,
    Risk = ToolRisk.High,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "pipes", "pipe-networks", "edit", "delete" })]
public sealed class DeletePipeTool : CommandToolBase<DeletePipeRequest, DeletePipeResult, DeletePipeCommand, DeletePipeResult>
{
    private readonly ISaveDrawingService? _save;
    private readonly bool _requireConfirmation;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="dispatcher">The command dispatcher (full pipeline).</param>
    /// <param name="save">Drawing save service; when provided, the drawing is saved after a successful deletion.</param>
    /// <param name="confirmations">Confirmation gate; defaults to deny.</param>
    /// <param name="undo">Undo context; defaults to no-op.</param>
    /// <param name="requireConfirmation">When true, the deletion requires explicit confirmation.</param>
    public DeletePipeTool(
        ICivil3DSession session,
        ICommandDispatcher dispatcher,
        ISaveDrawingService? save = null,
        IConfirmationGate? confirmations = null,
        IUndoContext? undo = null,
        bool requireConfirmation = false)
        : base(session, dispatcher, confirmations, undo)
    {
        _save = save;
        _requireConfirmation = requireConfirmation;
    }

    /// <inheritdoc />
    protected override DeletePipeCommand CreateCommand(DeletePipeRequest input, ToolExecutionContext context)
        => new()
        {
            PipeId = input.PipeId,
            RequiresConfirmation = _requireConfirmation,
        };

    /// <inheritdoc />
    /// <remarks>
    /// Runs after the command pipeline committed, so the saved state includes the deletion. The
    /// save is best-effort: a failed save never fails a successful delete.
    /// </remarks>
    protected override DeletePipeResult MapResult(DeletePipeResult result)
    {
        if (result.Success && _save is not null)
        {
            try
            {
                ActiveDrawing? drawing = Session.GetActiveDrawing();
                if (drawing is not null)
                {
                    _save.Save(drawing, zoomToExtents: false, CancellationToken.None);
                }
            }
            catch
            {
                // Best-effort: the delete already committed; keep the failure out of the response.
            }
        }

        return result;
    }
}
