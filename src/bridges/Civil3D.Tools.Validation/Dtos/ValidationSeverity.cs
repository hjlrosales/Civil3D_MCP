namespace Civil3D.Tools.Validation.Dtos;

/// <summary>
/// Severity of a validation finding, ordered from benign observation to blocking problem. The
/// report rolls counts up by severity so callers can prioritise work.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>Informational observation; no action required.</summary>
    Information = 0,

    /// <summary>Potential data-quality issue worth reviewing.</summary>
    Warning = 1,

    /// <summary>Definite problem that should be fixed.</summary>
    Error = 2,

    /// <summary>Problem likely to affect downstream production use.</summary>
    Critical = 3,
}
