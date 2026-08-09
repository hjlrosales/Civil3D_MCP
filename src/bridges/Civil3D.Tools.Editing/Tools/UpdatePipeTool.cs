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
/// Tool <c>update_pipe</c>: updates an existing pipe in a pipe network (elevation of both ends,
/// horizontal length along the pipe's current bearing, and/or inner diameter) through the full
/// command pipeline (validation → confirmation → write transaction → commit/rollback → domain
/// events → protocol response). At least one change must be provided; omitted changes keep the
/// pipe's current value. Fails with E_OBJECT_NOT_FOUND when the pipe id does not exist,
/// E_VALIDATION_FAILED when no change is requested, and E_TRANSACTION_FAILED when Civil 3D
/// rejects the change (for example pipe rules).
/// </summary>
[McpTool(
    "update_pipe",
    "Update Pipe",
    "Updates an existing pipe in a pipe network: elevation, length and/or diameter. Supply the " +
    "pipeId returned by create_pipe or list_pipe_networks, plus at least one of: ElevationMeters " +
    "(sets both the start and end elevation, keeping the pipe horizontal), LengthMeters (keeps the " +
    "start point fixed and moves the end point along the pipe's current horizontal bearing so the " +
    "length becomes that value, preserving the end elevation), and DiameterMm (resizes to the " +
    "available part size closest to that nominal inner diameter). Omitted changes keep the pipe's " +
    "current value. Fails with E_OBJECT_NOT_FOUND when the pipe does not exist and " +
    "E_VALIDATION_FAILED when no change is requested or a value is invalid.",
    Category = ToolCategory.PipeNetworks,
    Permission = ToolPermission.ModifyDrawing,
    Risk = ToolRisk.Medium,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "pipes", "pipe-networks", "edit", "update" })]
public sealed class UpdatePipeTool : CommandToolBase<UpdatePipeRequest, UpdatePipeResult, UpdatePipeCommand, UpdatePipeResult>
{
    private readonly bool _requireConfirmation;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="dispatcher">The command dispatcher (full pipeline).</param>
    /// <param name="confirmations">Confirmation gate; defaults to deny.</param>
    /// <param name="undo">Undo context; defaults to no-op.</param>
    /// <param name="requireConfirmation">When true, the update requires explicit confirmation.</param>
    public UpdatePipeTool(
        ICivil3DSession session,
        ICommandDispatcher dispatcher,
        IConfirmationGate? confirmations = null,
        IUndoContext? undo = null,
        bool requireConfirmation = false)
        : base(session, dispatcher, confirmations, undo)
    {
        _requireConfirmation = requireConfirmation;
    }

    /// <inheritdoc />
    protected override UpdatePipeCommand CreateCommand(UpdatePipeRequest input, ToolExecutionContext context)
        => new()
        {
            PipeId = input.PipeId,
            ElevationMeters = input.ElevationMeters,
            LengthMeters = input.LengthMeters,
            DiameterMm = input.DiameterMm,
            RequiresConfirmation = _requireConfirmation,
        };

    /// <inheritdoc />
    protected override UpdatePipeResult MapResult(UpdatePipeResult result) => result;
}
