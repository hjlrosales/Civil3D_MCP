namespace Civil3D.Tools.Export.Abstractions;

/// <summary>
/// Writes Civil 3D objects into a LandXML file. The workflow depends only on this contract,
/// never on Autodesk export APIs; the production implementation isolates the platform's export
/// capability (or its absence) behind this boundary so tests can substitute fakes.
/// </summary>
public interface ILandXmlExporter
{
    /// <summary>Exports the included object types to <see cref="LandXmlExportData.OutputPath"/>.</summary>
    /// <param name="data">The validated request options and collected object counts.</param>
    /// <returns>The export outcome; never throws for unsupported object types (those are skipped).</returns>
    LandXmlExportResult Export(LandXmlExportData data);
}
