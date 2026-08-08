namespace Civil3D.Tools.Project.Dtos;

/// <summary>
/// Reference integrity of the drawing: external references plus object-to-object references
/// (profiles to alignments, corridors to alignments and styles). No Autodesk types.
/// </summary>
public sealed record ReferenceSummary
{
    /// <summary>The number of external references (xrefs).</summary>
    public int TotalXRefs { get; init; }

    /// <summary>The total number of references checked (xrefs + object references).</summary>
    public int TotalReferencesChecked { get; init; }

    /// <summary>The number of references that resolved to a valid target.</summary>
    public int HealthyReferenceCount { get; init; }

    /// <summary>The number of references that failed to resolve.</summary>
    public int MissingReferenceCount { get; init; }

    /// <summary>The number of objects referencing a non-existent parent (orphaned).</summary>
    public int OrphanedObjectCount { get; init; }

    /// <summary>The number of objects referencing a non-existent style.</summary>
    public int MissingStyleCount { get; init; }

    /// <summary>True when every reference resolved; false when any reference is missing.</summary>
    public bool IsHealthy { get; init; }

    /// <summary>Human-readable status: <c>Healthy</c> or <c>Issues Found</c>.</summary>
    public string Status { get; init; } = string.Empty;
}
