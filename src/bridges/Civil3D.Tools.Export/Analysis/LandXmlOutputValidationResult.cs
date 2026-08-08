namespace Civil3D.Tools.Export.Analysis;

/// <summary>
/// The result of validating an exported file: existence, size and basic XML well-formedness.
/// Full LandXML schema validation is out of scope for this phase.
/// </summary>
public sealed record LandXmlOutputValidationResult
{
    /// <summary>True when a file exists at the expected path.</summary>
    public bool Exists { get; init; }

    /// <summary>Size of the file in bytes; 0 when missing.</summary>
    public long FileSizeBytes { get; init; }

    /// <summary>True when the file parses as well-formed XML.</summary>
    public bool IsWellFormedXml { get; init; }

    /// <summary>True when the file exists, is non-empty and parses as well-formed XML.</summary>
    public bool IsValid => Exists && FileSizeBytes > 0 && IsWellFormedXml;
}
