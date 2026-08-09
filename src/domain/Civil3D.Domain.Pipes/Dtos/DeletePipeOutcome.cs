namespace Civil3D.Domain.Pipes.Dtos;

/// <summary>
/// The outcome of a pipe deletion performed by a write repository: the identity of the deleted
/// pipe (read back before it was erased) and its owning network. Autodesk-free; produced inside
/// the write transaction.
/// </summary>
public sealed record DeletePipeOutcome
{
    /// <summary>Stable numeric id of the deleted pipe.</summary>
    public long PipeId { get; init; }

    /// <summary>The deleted pipe name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Stable numeric id of the owning network.</summary>
    public long NetworkId { get; init; }

    /// <summary>Name of the owning network.</summary>
    public string NetworkName { get; init; } = string.Empty;

    /// <summary>Description of the pipe part family (read before deletion).</summary>
    public string PartFamilyName { get; init; } = string.Empty;

    /// <summary>Name of the selected part size (read before deletion).</summary>
    public string PartSizeName { get; init; } = string.Empty;
}
