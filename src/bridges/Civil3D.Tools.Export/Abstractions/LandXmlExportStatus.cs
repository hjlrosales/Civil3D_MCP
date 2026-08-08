namespace Civil3D.Tools.Export.Abstractions;

/// <summary>
/// Whether the exporter wrote the LandXML file or reported that no reliable export path exists.
/// </summary>
public enum LandXmlExportStatus
{
    /// <summary>The LandXML file was written successfully.</summary>
    Exported = 0,

    /// <summary>
    /// The underlying API does not expose a reliable export path; the result carries a structured
    /// reason instead of invented output.
    /// </summary>
    NotSupported = 1,
}
