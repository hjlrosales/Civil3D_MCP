using System.Globalization;
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
/// Tool <c>create_pipe</c>: creates a straight pipe in an existing pipe network through the full
/// command pipeline (validation → confirmation → write transaction → commit/rollback → domain
/// events → protocol response). Inherits <see cref="CommandToolBase{TIn,TOut,TCommand,TResult}"/>
/// so no orchestration is duplicated.
/// </summary>
[McpTool(
    "create_pipe",
    "Create Pipe",
    "Creates a straight pipe in an existing pipe network. The pipe part is resolved by matching " +
    "(case-insensitive, substring) against the pipe part family descriptions already assigned to " +
    "the network's parts list: supply Material (and optionally Sdr / PressureClassBar, for " +
    "example HDPE / 17 / 10) or an explicit PartFamilyMatch. The closest available size to " +
    "DiameterMm is selected automatically. The pipe runs horizontally (constant elevation) for " +
    "LengthMeters starting at (StartEasting, StartNorthing, StartElevation), in the plan " +
    "direction DirectionDegrees (0 = +Easting axis, counter-clockwise). Fails with " +
    "E_OBJECT_NOT_FOUND when the network does not exist, E_VALIDATION_FAILED when no single part " +
    "family matches, and E_TRANSACTION_FAILED on write failure.",
    Category = ToolCategory.PipeNetworks,
    Permission = ToolPermission.ModifyDrawing,
    Risk = ToolRisk.Medium,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "pipes", "pipe-networks", "edit", "create" })]
public sealed class CreatePipeTool : CommandToolBase<CreatePipeRequest, CreatePipeResult, CreatePipeCommand, CreatePipeResult>
{
    private readonly bool _requireConfirmation;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="dispatcher">The command dispatcher (full pipeline).</param>
    /// <param name="confirmations">Confirmation gate; defaults to deny.</param>
    /// <param name="undo">Undo context; defaults to no-op.</param>
    /// <param name="requireConfirmation">When true, the creation requires explicit confirmation.</param>
    public CreatePipeTool(
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
    protected override CreatePipeCommand CreateCommand(CreatePipeRequest input, ToolExecutionContext context)
    {
        string partFamilyMatch = string.IsNullOrWhiteSpace(input.PartFamilyMatch)
            ? BuildDefaultPartFamilyMatch(input)
            : input.PartFamilyMatch.Trim();

        double directionRadians = input.DirectionDegrees * Math.PI / 180.0;
        double endEasting = input.StartEasting + (input.LengthMeters * Math.Cos(directionRadians));
        double endNorthing = input.StartNorthing + (input.LengthMeters * Math.Sin(directionRadians));

        return new CreatePipeCommand
        {
            NetworkName = input.NetworkName,
            PartFamilyMatch = partFamilyMatch,
            DiameterMm = input.DiameterMm,
            LengthMeters = input.LengthMeters,
            StartEasting = input.StartEasting,
            StartNorthing = input.StartNorthing,
            StartElevation = input.StartElevation,
            EndEasting = endEasting,
            EndNorthing = endNorthing,
            Description = input.Description,
            RequiresConfirmation = _requireConfirmation,
        };
    }

    /// <inheritdoc />
    protected override CreatePipeResult MapResult(CreatePipeResult result) => result;

    /// <summary>
    /// Builds the default part family search text from the discrete material/SDR/pressure-class
    /// fields (for example "HDPE" + "17" + 10 → "HDPE SDR17 PN10"), used when the caller does not
    /// supply an explicit <see cref="CreatePipeRequest.PartFamilyMatch"/>.
    /// </summary>
    private static string BuildDefaultPartFamilyMatch(CreatePipeRequest input)
    {
        var terms = new List<string>();
        if (!string.IsNullOrWhiteSpace(input.Material))
        {
            terms.Add(input.Material.Trim());
        }

        if (!string.IsNullOrWhiteSpace(input.Sdr))
        {
            terms.Add($"SDR{input.Sdr.Trim()}");
        }

        if (input.PressureClassBar is { } pressureClassBar)
        {
            terms.Add($"PN{pressureClassBar.ToString("0.#", CultureInfo.InvariantCulture)}");
        }

        return string.Join(' ', terms);
    }
}
