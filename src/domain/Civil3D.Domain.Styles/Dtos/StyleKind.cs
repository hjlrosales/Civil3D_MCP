namespace Civil3D.Domain.Styles.Dtos;

/// <summary>
/// Stable, serializable classification of a Civil 3D style, based on which style collection the
/// style belongs to.
/// </summary>
public enum StyleKind
{
    /// <summary>Alignment style.</summary>
    Alignment,

    /// <summary>Surface style.</summary>
    Surface,

    /// <summary>Corridor style.</summary>
    Corridor,

    /// <summary>Pipe style.</summary>
    Pipe,

    /// <summary>Structure style.</summary>
    Structure,

    /// <summary>Profile style.</summary>
    Profile,

    /// <summary>COGO point style.</summary>
    Point,

    /// <summary>Feature line style.</summary>
    FeatureLine,

    /// <summary>Any style type not covered by the values above.</summary>
    Other,
}
