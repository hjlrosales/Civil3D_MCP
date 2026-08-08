namespace Civil3D.Tools.Project.Dtos;

/// <summary>
/// The complexity classification of a drawing, derived from object counts, entity volume and
/// reference counts via the configured thresholds. Ordered from simplest to most complex.
/// </summary>
public enum ProjectComplexity
{
    /// <summary>A small, low-volume drawing.</summary>
    Small = 0,

    /// <summary>A moderately sized drawing.</summary>
    Medium = 1,

    /// <summary>A large drawing with substantial object volume.</summary>
    Large = 2,

    /// <summary>A very large, high-complexity drawing.</summary>
    Enterprise = 3,
}
