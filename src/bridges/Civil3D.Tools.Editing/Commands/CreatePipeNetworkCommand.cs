using Civil3D.Domain.Commands;
using Civil3D.Domain.Pipes.Dtos;

namespace Civil3D.Tools.Editing.Commands;

/// <summary>Creates a new pipe network (with parts list) in the active drawing.</summary>
public sealed class CreatePipeNetworkCommand : ICommand<CreatePipeNetworkResult>
{
    /// <inheritdoc />
    public string Name => "create.pipeNetwork";

    /// <inheritdoc />
    public CommandPermission RequiredPermission => CommandPermission.ModifyDrawing;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    public ConfirmationDescriptor? Confirmation => RequiresConfirmation
        ? new ConfirmationDescriptor
        {
            Title = "Create Pipe Network",
            Message = $"Create pipe network '{NetworkName}' with parts list '{PartsListName}'?",
            Risk = "Medium",
        }
        : null;

    /// <summary>Whether this creation requires user confirmation (driven by bridge policy).</summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>Name of the pipe network to create.</summary>
    public string NetworkName { get; init; } = string.Empty;

    /// <summary>Optional free-text description to set on the network.</summary>
    public string? Description { get; init; }

    /// <summary>Name of the parts list to use (created when it does not exist).</summary>
    public string? PartsListName { get; init; }

    /// <summary>Pipe materials whose part families are added to the parts list.</summary>
    public IReadOnlyList<string> Materials { get; init; } = Array.Empty<string>();

    /// <summary>Nominal inner diameters (millimetres) to add as sizes to the added families.</summary>
    public IReadOnlyList<double> SizesMm { get; init; } = Array.Empty<double>();
}
