namespace Civil3D.Tools.Editing.Dtos;

/// <summary>Input of <c>rename_alignment</c>: the alignment's stable numeric id and the new name.</summary>
public sealed record RenameAlignmentRequest
{
    /// <summary>Stable numeric id of the alignment to rename.</summary>
    public long ObjectId { get; init; }

    /// <summary>The new alignment name.</summary>
    public string NewName { get; init; } = string.Empty;
}

/// <summary>Input of <c>rename_surface</c>: the surface's stable numeric id and the new name.</summary>
public sealed record RenameSurfaceRequest
{
    /// <summary>Stable numeric id of the surface to rename.</summary>
    public long ObjectId { get; init; }

    /// <summary>The new surface name.</summary>
    public string NewName { get; init; } = string.Empty;
}
