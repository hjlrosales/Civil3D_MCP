namespace Civil3D.Tools.CutFill.Abstractions;

/// <summary>
/// Whether the calculator produced volumes or reported that no reliable volume path exists.
/// </summary>
public enum CutFillStatus
{
    /// <summary>Volumes were computed successfully.</summary>
    Computed = 0,

    /// <summary>
    /// The underlying API does not expose a reliable read-only volume path; the result carries a
    /// structured reason instead of invented numbers.
    /// </summary>
    NotSupported = 1,
}
