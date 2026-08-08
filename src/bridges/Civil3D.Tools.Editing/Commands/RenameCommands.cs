using Civil3D.Domain.Commands;

namespace Civil3D.Tools.Editing.Commands;

/// <summary>
/// Common shape of the rename commands. Each discipline command contributes only its name and
/// the object id/new name payload; all pipeline concerns (permission, read-only, confirmation)
/// are shared here so the two commands cannot drift.
/// </summary>
public abstract class RenameCommandBase : ICommand<RenameResult>
{
    /// <summary>The stable command name (overridden per discipline).</summary>
    public abstract string Name { get; }

    /// <inheritdoc />
    public CommandPermission RequiredPermission => CommandPermission.ModifyDrawing;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <summary>Whether this rename requires user confirmation (driven by bridge policy).</summary>
    public bool RequiresConfirmation { get; init; }

    /// <inheritdoc />
    public ConfirmationDescriptor? Confirmation => RequiresConfirmation
        ? new ConfirmationDescriptor
        {
            Title = $"Rename {ObjectType}",
            Message = $"Rename '{PreviousName ?? ObjectType}' to '{NewName}'?",
            Risk = "Medium",
        }
        : null;

    /// <summary>The discipline label used in confirmation text and events ("alignment", "surface").</summary>
    protected abstract string ObjectType { get; }

    /// <summary>Stable numeric id of the object to rename.</summary>
    public long ObjectId { get; init; }

    /// <summary>The current name, captured by the tool so a no-op rename is reported clearly.</summary>
    public string? PreviousName { get; init; }

    /// <summary>The new name.</summary>
    public string NewName { get; init; } = string.Empty;
}

/// <summary>Renames an alignment (Phase 5B).</summary>
public sealed class RenameAlignmentCommand : RenameCommandBase
{
    /// <inheritdoc />
    public override string Name => "rename.alignment";

    /// <inheritdoc />
    protected override string ObjectType => "alignment";
}

/// <summary>Renames a surface (Phase 5B).</summary>
public sealed class RenameSurfaceCommand : RenameCommandBase
{
    /// <inheritdoc />
    public override string Name => "rename.surface";

    /// <inheritdoc />
    protected override string ObjectType => "surface";
}
