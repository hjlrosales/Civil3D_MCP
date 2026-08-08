namespace Civil3D.Domain.Commands;

/// <summary>
/// Immutable outcome of a rename command. Autodesk-free and serializable; the tool layer maps it
/// directly to the protocol response.
/// </summary>
public sealed record RenameResult
{
    /// <summary>Stable numeric id of the renamed object.</summary>
    public long ObjectId { get; init; }

    /// <summary>The name before the rename.</summary>
    public string PreviousName { get; init; } = string.Empty;

    /// <summary>The name after the rename.</summary>
    public string CurrentName { get; init; } = string.Empty;

    /// <summary>True when the rename was applied (always true on a successful command).</summary>
    public bool Success { get; init; }

    /// <summary>UTC timestamp of the rename.</summary>
    public DateTime TimestampUtc { get; init; }
}
