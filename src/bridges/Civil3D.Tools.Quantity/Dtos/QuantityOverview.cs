namespace Civil3D.Tools.Quantity.Dtos;

/// <summary>
/// The drawing identity section of the quantity takeoff report, copied from the active-drawing
/// snapshot so the report is self-contained and Autodesk-free.
/// </summary>
public sealed record QuantityOverview
{
    /// <summary>The file name of the inspected drawing.</summary>
    public string DrawingName { get; init; } = string.Empty;

    /// <summary>The full path of the inspected drawing.</summary>
    public string DrawingPath { get; init; } = string.Empty;

    /// <summary>The DWG file format version, for example <c>AC1032</c>.</summary>
    public string DrawingVersion { get; init; } = string.Empty;

    /// <summary>The host Civil 3D application version, for example <c>25.0</c>.</summary>
    public string Civil3DVersion { get; init; } = string.Empty;

    /// <summary>True when the drawing contains unsaved changes.</summary>
    public bool IsModified { get; init; }

    /// <summary>True when the drawing file is read-only.</summary>
    public bool IsReadOnly { get; init; }

    /// <summary>True when model space is the active space.</summary>
    public bool IsModelSpaceActive { get; init; }

    /// <summary>A stable fingerprint of the database content.</summary>
    public string DatabaseFingerprint { get; init; } = string.Empty;

    /// <summary>The number of documents currently open in the application.</summary>
    public int OpenDocumentsCount { get; init; }
}
