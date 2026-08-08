namespace Civil3D.Tools.Abstractions;

/// <summary>
/// Immutable snapshot of the active drawing taken once per invocation. Produced by
/// <see cref="ICivil3DSession.GetActiveDrawing"/>; tools map it (plus version metadata) into their
/// wire DTOs. Contains no Autodesk types so it can be freely constructed in tests.
/// </summary>
public sealed record ActiveDrawing
{
    /// <summary>The file name of the active drawing, for example <c>Site-Plan.dwg</c>.</summary>
    public string DrawingName { get; init; } = string.Empty;

    /// <summary>The full path of the active drawing file.</summary>
    public string DrawingPath { get; init; } = string.Empty;

    /// <summary>The DWG file format version, for example <c>AC1032</c> (AutoCAD 2025).</summary>
    public string DrawingVersion { get; init; } = string.Empty;

    /// <summary>True when the drawing contains unsaved changes (DBMOD &gt; 0).</summary>
    public bool IsModified { get; init; }

    /// <summary>True when the drawing file is read-only.</summary>
    public bool IsReadOnly { get; init; }

    /// <summary>The name of the currently active layout, for example <c>Model</c>.</summary>
    public string CurrentLayout { get; init; } = string.Empty;

    /// <summary>True when model space is the active space (tiled mode).</summary>
    public bool IsModelSpaceActive { get; init; }

    /// <summary>A stable fingerprint of the database content, for change detection.</summary>
    public string DatabaseFingerprint { get; init; } = string.Empty;

    /// <summary>The version of the host Civil 3D application, for example <c>25.0</c>.</summary>
    public string Civil3DVersion { get; init; } = string.Empty;

    /// <summary>The number of documents currently open in the application.</summary>
    public int OpenDocumentsCount { get; init; }

    /// <summary>The file name of the current document (equals <see cref="DrawingName"/> for the active document).</summary>
    public string CurrentDocumentName { get; init; } = string.Empty;

    /// <summary>The full path of the current document (equals <see cref="DrawingPath"/> for the active document).</summary>
    public string CurrentDocumentPath { get; init; } = string.Empty;
}
