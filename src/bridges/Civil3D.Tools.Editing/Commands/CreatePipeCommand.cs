using Civil3D.Domain.Commands;
using Civil3D.Domain.Pipes.Dtos;

namespace Civil3D.Tools.Editing.Commands;

/// <summary>Creates a straight (line) pipe in an existing pipe network (Phase 5C).</summary>
public sealed class CreatePipeCommand : ICommand<CreatePipeResult>
{
    /// <inheritdoc />
    public string Name => "create.pipe";

    /// <inheritdoc />
    public CommandPermission RequiredPermission => CommandPermission.ModifyDrawing;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <summary>Whether this creation requires user confirmation (driven by bridge policy).</summary>
    public bool RequiresConfirmation { get; init; }

    /// <inheritdoc />
    public ConfirmationDescriptor? Confirmation => RequiresConfirmation
        ? new ConfirmationDescriptor
        {
            Title = "Create Pipe",
            Message = $"Create a {LengthMeters:0.###} m pipe in network '{NetworkName}' " +
                      $"({PartFamilyMatch}, {DiameterMm:0.#} mm)?",
            Risk = "Medium",
        }
        : null;

    /// <summary>Name of the existing pipe network to add the pipe to.</summary>
    public string NetworkName { get; init; } = string.Empty;

    /// <summary>Text matched against the network's pipe part family descriptions.</summary>
    public string PartFamilyMatch { get; init; } = string.Empty;

    /// <summary>Target nominal/inner diameter in millimeters.</summary>
    public double DiameterMm { get; init; }

    /// <summary>Horizontal run length in meters, used only for the confirmation message.</summary>
    public double LengthMeters { get; init; }

    /// <summary>Start point easting.</summary>
    public double StartEasting { get; init; }

    /// <summary>Start point northing.</summary>
    public double StartNorthing { get; init; }

    /// <summary>Start and end point elevation (the pipe runs horizontally).</summary>
    public double StartElevation { get; init; }

    /// <summary>End point easting.</summary>
    public double EndEasting { get; init; }

    /// <summary>End point northing.</summary>
    public double EndNorthing { get; init; }

    /// <summary>Optional free-text description to set on the created pipe.</summary>
    public string? Description { get; init; }
}
