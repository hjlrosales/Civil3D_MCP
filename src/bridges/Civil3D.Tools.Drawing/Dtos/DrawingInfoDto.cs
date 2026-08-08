namespace Civil3D.Tools.Drawing.Dtos;

/// <summary>
/// The immutable result of <c>drawing_info</c>: identity, state and version metadata of the active
/// drawing. Serialized to the wire by the dispatcher; never mutated after creation.
/// </summary>
public sealed record DrawingInfoDto
{
    /// <summary>The file name of the active drawing, for example <c>Site-Plan.dwg</c>.</summary>
    public string DrawingName { get; init; } = string.Empty;

    /// <summary>The full path of the active drawing file.</summary>
    public string DrawingPath { get; init; } = string.Empty;

    /// <summary>The DWG file format version, for example <c>AC1032</c> (AutoCAD 2025).</summary>
    public string DrawingVersion { get; init; } = string.Empty;

    /// <summary>True when the drawing contains unsaved changes.</summary>
    public bool IsModified { get; init; }

    /// <summary>True when the drawing file is read-only.</summary>
    public bool IsReadOnly { get; init; }

    /// <summary>The name of the currently active layout, for example <c>Model</c>.</summary>
    public string CurrentLayout { get; init; } = string.Empty;

    /// <summary>True when model space is the active space.</summary>
    public bool IsModelSpaceActive { get; init; }

    /// <summary>A stable fingerprint of the database content, for change detection.</summary>
    public string DatabaseFingerprint { get; init; } = string.Empty;

    /// <summary>The version of the host Civil 3D application, for example <c>25.0</c> for Civil 3D 2025.</summary>
    public string Civil3DVersion { get; init; } = string.Empty;

    /// <summary>The semantic version of the bridge assembly.</summary>
    public string BridgeVersion { get; init; } = string.Empty;

    /// <summary>The protocol version spoken by the bridge (semantic version string).</summary>
    public string ProtocolVersion { get; init; } = string.Empty;

    /// <summary>The semantic version of the SDK the bridge is built against.</summary>
    public string SdkVersion { get; init; } = string.Empty;

    /// <summary>The number of documents currently open in the application.</summary>
    public int OpenDocumentsCount { get; init; }

    /// <summary>The file name of the current document (equals <see cref="DrawingName"/> for the active document).</summary>
    public string CurrentDocumentName { get; init; } = string.Empty;

    /// <summary>The full path of the current document (equals <see cref="DrawingPath"/> for the active document).</summary>
    public string CurrentDocumentPath { get; init; } = string.Empty;
}
