namespace Civil3D.Domain.Pipes.Dtos;

/// <summary>
/// Immutable outcome of a delete-pipe command. Autodesk-free and serializable; the tool layer
/// maps it directly to the protocol response.
/// </summary>
public sealed record DeletePipeResult
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

    /// <summary>True when the pipe was deleted (always true on a successful command).</summary>
    public bool Success { get; init; }

    /// <summary>UTC timestamp of the deletion.</summary>
    public DateTime DeletedAtUtc { get; init; }
}
