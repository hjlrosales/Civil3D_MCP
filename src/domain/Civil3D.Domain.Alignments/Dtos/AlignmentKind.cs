namespace Civil3D.Domain.Alignments.Dtos;

/// <summary>
/// Stable, serializable classification of an alignment, mapped from the Autodesk
/// <c>AlignmentType</c> by the data source. Unknown values map to <see cref="Other"/>.
/// </summary>
public enum AlignmentKind
{
    /// <summary>Standard centerline alignment.</summary>
    Centerline,

    /// <summary>Offset alignment generated from a parent alignment.</summary>
    Offset,

    /// <summary>Curb-return alignment at an intersection.</summary>
    CurbReturn,

    /// <summary>Utility alignment.</summary>
    Utility,

    /// <summary>Rail alignment.</summary>
    Rail,

    /// <summary>Any alignment type not covered by the values above.</summary>
    Other,
}
