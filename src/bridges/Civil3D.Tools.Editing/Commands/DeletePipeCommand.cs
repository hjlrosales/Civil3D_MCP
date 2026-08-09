using Civil3D.Domain.Commands;
using Civil3D.Domain.Pipes.Dtos;

namespace Civil3D.Tools.Editing.Commands;

/// <summary>Deletes an existing pipe from its pipe network.</summary>
public sealed class DeletePipeCommand : ICommand<DeletePipeResult>
{
    /// <inheritdoc />
    public string Name => "delete.pipe";

    /// <inheritdoc />
    public CommandPermission RequiredPermission => CommandPermission.ModifyDrawing;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    public ConfirmationDescriptor? Confirmation => RequiresConfirmation
        ? new ConfirmationDescriptor
        {
            Title = "Delete Pipe",
            Message = $"Delete pipe with id {PipeId} from its network? This cannot be undone.",
            Risk = "High",
        }
        : null;

    /// <summary>Whether this deletion requires user confirmation (driven by bridge policy).</summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>Stable numeric id of the pipe to delete.</summary>
    public long PipeId { get; init; }
}
