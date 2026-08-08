namespace Civil3D.Tools.CutFill.Dtos;

/// <summary>
/// Severity of a cut/fill finding, ordered from benign observation to blocking problem. Used on
/// recommendations so callers can prioritise review.
/// </summary>
public enum CutFillSeverity
{
    /// <summary>Informational observation; no action required.</summary>
    Information = 0,

    /// <summary>Potential data-quality issue worth reviewing.</summary>
    Warning = 1,

    /// <summary>Definite problem that should be resolved.</summary>
    Error = 2,

    /// <summary>Problem likely to affect downstream production use.</summary>
    Critical = 3,
}
