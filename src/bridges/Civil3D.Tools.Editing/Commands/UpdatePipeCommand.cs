using Civil3D.Domain.Commands;
using Civil3D.Domain.Pipes.Dtos;

namespace Civil3D.Tools.Editing.Commands;

/// <summary>Updates an existing pipe in a pipe network (elevation, length and/or diameter).</summary>
public sealed class UpdatePipeCommand : ICommand<UpdatePipeResult>
{
    /// <inheritdoc />
    public string Name => "update.pipe";

    /// <inheritdoc />
    public CommandPermission RequiredPermission => CommandPermission.ModifyDrawing;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    public ConfirmationDescriptor? Confirmation => RequiresConfirmation
        ? new ConfirmationDescriptor
        {
            Title = "Update Pipe",
            Message = $"Update pipe '{PipeName}' ({DescribeChanges})?",
            Risk = "Medium",
        }
        : null;

    /// <summary>Whether this update requires user confirmation (driven by bridge policy).</summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>Stable numeric id of the pipe to update.</summary>
    public long PipeId { get; init; }

    /// <summary>The pipe name, used only for the confirmation message.</summary>
    public string PipeName { get; init; } = string.Empty;

    /// <summary>When set, both end elevations become this value.</summary>
    public double? ElevationMeters { get; init; }

    /// <summary>When set, the pipe is rescaled to this horizontal length along its current bearing.</summary>
    public double? LengthMeters { get; init; }

    /// <summary>When set, the pipe is resized to the closest available inner diameter (mm).</summary>
    public double? DiameterMm { get; init; }

    private string DescribeChanges()
    {
        var terms = new List<string>();
        if (ElevationMeters is { } e) terms.Add($"elevation {e:0.###} m");
        if (LengthMeters is { } l) terms.Add($"length {l:0.###} m");
        if (DiameterMm is { } d) terms.Add($"diameter {d:0.#} mm");
        return string.Join(", ", terms);
    }
}
