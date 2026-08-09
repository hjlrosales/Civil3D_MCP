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
/// Tool <c>create_pipe_network</c>: creates a new gravity pipe network in the current drawing,
/// creating (or reusing) the parts list and adding the requested pipe material families from the
/// installed Civil 3D pipe catalog, through the full command pipeline (validation → confirmation
/// → write transaction → commit/rollback → domain events → protocol response). Run this before
/// <c>create_pipe</c> when the drawing has no pipe networks. Fails with E_VALIDATION_FAILED when
/// the network name is missing or already exists.
/// </summary>
[McpTool(
    "create_pipe_network",
    "Create Pipe Network",
    "Creates a new pipe network in the current drawing, with a parts list that includes the " +
    "requested pipe material families (for example HDPE, PVC, Ductile Iron, Concrete/RCP, " +
    "corrugated metal) taken from " +
    "the installed Civil 3D pipe catalog, then assigns the parts list to the network. Materials " +
    "without a matching catalog family are skipped and reported in familiesFailed. Optional " +
    "sizesMm adds nominal inner diameters (millimetres) as sizes to the added families so a later " +
    "create_pipe can snap to that diameter. Use this before create_pipe when the drawing has no " +
    "pipe networks. Fails with E_VALIDATION_FAILED when the network name is missing or a network " +
    "with that name already exists.",
    Category = ToolCategory.PipeNetworks,
    Permission = ToolPermission.ModifyDrawing,
    Risk = ToolRisk.Medium,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "pipes", "pipe-networks", "edit", "create" })]
public sealed class CreatePipeNetworkTool : CommandToolBase<CreatePipeNetworkRequest, CreatePipeNetworkResult, CreatePipeNetworkCommand, CreatePipeNetworkResult>
{
    private static readonly string[] DefaultMaterials = ["HDPE", "PVC", "Concrete", "Ductile Iron"];

    // AddPartFamilyByDescription adds a family shell with no sizes in current Civil 3D versions,
    // so when no sizes are requested the tool adds the common gravity-pipe diameter range instead
    // of leaving families empty (which would make a later create_pipe fail with "no sizes").
    private static readonly double[] DefaultSizesMm = [100, 150, 200, 250, 300];

    private readonly bool _requireConfirmation;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="dispatcher">The command dispatcher (full pipeline).</param>
    /// <param name="confirmations">Confirmation gate; defaults to deny.</param>
    /// <param name="undo">Undo context; defaults to no-op.</param>
    /// <param name="requireConfirmation">When true, the creation requires explicit confirmation.</param>
    public CreatePipeNetworkTool(
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
    protected override CreatePipeNetworkCommand CreateCommand(CreatePipeNetworkRequest input, ToolExecutionContext context)
    {
        // Trimmed but otherwise unmodified: blank entries are caught by the structural validator.
        string[] materials = input.Materials is { Length: > 0 }
            ? input.Materials.Select(m => m.Trim()).ToArray()
            : DefaultMaterials;

        double[] sizesMm = input.SizesMm is { Length: > 0 }
            ? input.SizesMm
            : DefaultSizesMm;

        return new CreatePipeNetworkCommand
        {
            NetworkName = input.Name.Trim(),
            Description = input.Description,
            PartsListName = string.IsNullOrWhiteSpace(input.PartsListName) ? null : input.PartsListName.Trim(),
            Materials = materials,
            SizesMm = sizesMm,
            RequiresConfirmation = _requireConfirmation,
        };
    }

    /// <inheritdoc />
    protected override CreatePipeNetworkResult MapResult(CreatePipeNetworkResult result) => result;
}
