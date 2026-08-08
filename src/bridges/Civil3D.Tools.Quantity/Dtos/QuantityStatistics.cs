namespace Civil3D.Tools.Quantity.Dtos;

/// <summary>
/// Aggregate statistics across all disciplines: total domain objects, total measured linear
/// length, total pipes/structures/surface points/corridor surfaces and the drawing entity
/// volume. Every value is derived from the existing domain DTOs; metrics that are not available
/// through the current services are omitted rather than invented.
/// </summary>
public sealed record QuantityStatistics
{
    /// <summary>The total number of domain objects across all inspected collections.</summary>
    public int TotalDomainObjects { get; init; }

    /// <summary>The total measured linear length of alignments and profiles, in drawing units.</summary>
    public double TotalLinearLength { get; init; }

    /// <summary>The total number of surface definition points.</summary>
    public int TotalSurfacePoints { get; init; }

    /// <summary>The total number of corridor baselines.</summary>
    public int TotalCorridorBaselines { get; init; }

    /// <summary>The total number of corridor surfaces.</summary>
    public int TotalCorridorSurfaces { get; init; }

    /// <summary>The total number of pipes across all pipe networks.</summary>
    public int TotalPipes { get; init; }

    /// <summary>The total number of structures across all pipe networks.</summary>
    public int TotalStructures { get; init; }

    /// <summary>The total number of locked COGO points.</summary>
    public int LockedCogoPointCount { get; init; }

    /// <summary>The total number of drawing entities (model and paper space).</summary>
    public int TotalEntities { get; init; }

    /// <summary>The approximate on-disk drawing size in bytes; 0 when unavailable.</summary>
    public long ApproximateDrawingSizeBytes { get; init; }
}
