namespace Civil3D.Tools.Project.Dtos;

/// <summary>
/// The drawing metadata section of the project summary: identity, state and version information.
/// A subset of the drawing snapshot that gives clients immediate context.
/// </summary>
public sealed record ProjectOverview
{
    /// <summary>The file name of the drawing.</summary>
    public string DrawingName { get; init; } = string.Empty;

    /// <summary>The full path of the drawing file.</summary>
    public string DrawingPath { get; init; } = string.Empty;

    /// <summary>The DWG file format version, for example <c>AC1032</c>.</summary>
    public string DrawingVersion { get; init; } = string.Empty;

    /// <summary>The host Civil 3D version, for example <c>25.0</c>.</summary>
    public string Civil3DVersion { get; init; } = string.Empty;

    /// <summary>True when the drawing contains unsaved changes.</summary>
    public bool IsModified { get; init; }

    /// <summary>True when the drawing file is read-only.</summary>
    public bool IsReadOnly { get; init; }

    /// <summary>The name of the currently active layout.</summary>
    public string CurrentLayout { get; init; } = string.Empty;

    /// <summary>A stable fingerprint of the database content, for change detection.</summary>
    public string DatabaseFingerprint { get; init; } = string.Empty;

    /// <summary>The number of documents currently open in the application.</summary>
    public int OpenDocumentsCount { get; init; }
}
