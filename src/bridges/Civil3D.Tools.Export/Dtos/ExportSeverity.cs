namespace Civil3D.Tools.Export.Dtos;

/// <summary>
/// Severity of an export finding, ordered from benign observation to blocking problem. Used on
/// recommendations so callers can prioritise review.
/// </summary>
public enum ExportSeverity
{
    /// <summary>Informational observation; no action required.</summary>
    Information = 0,

    /// <summary>Potential issue worth reviewing.</summary>
    Warning = 1,

    /// <summary>Definite problem that should be resolved.</summary>
    Error = 2,

    /// <summary>Problem that blocks downstream use.</summary>
    Critical = 3,
}
