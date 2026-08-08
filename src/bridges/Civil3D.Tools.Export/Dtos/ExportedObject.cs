namespace Civil3D.Tools.Export.Dtos;

/// <summary>
/// One object the exporter successfully wrote into the LandXML file.
/// </summary>
public sealed record ExportedObject
{
    /// <summary>The object type, for example <c>Alignment</c>.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>The object name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The object id, when available.</summary>
    public long Id { get; init; }
}
