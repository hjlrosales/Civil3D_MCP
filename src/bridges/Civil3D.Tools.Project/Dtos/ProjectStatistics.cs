namespace Civil3D.Tools.Project.Dtos;

/// <summary>
/// The top-level totals of the project summary.
/// </summary>
public sealed record ProjectStatistics
{
    /// <summary>The total number of domain objects (alignments, profiles, surfaces, corridors,
    /// networks, points, styles).</summary>
    public int TotalDomainObjects { get; init; }

    /// <summary>The total number of drawing entities.</summary>
    public int TotalEntities { get; init; }

    /// <summary>The total number of external references.</summary>
    public int TotalXRefs { get; init; }

    /// <summary>The total number of references checked for integrity.</summary>
    public int TotalReferencesChecked { get; init; }

    /// <summary>The number of references that resolved correctly.</summary>
    public int HealthyReferenceCount { get; init; }

    /// <summary>The number of missing or broken references.</summary>
    public int MissingReferenceCount { get; init; }
}
