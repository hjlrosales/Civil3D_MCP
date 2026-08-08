using Microsoft.Extensions.Logging;

namespace Civil3D.Tools.Export.Abstractions;

/// <summary>
/// The production ILandXmlExporter. The Civil 3D managed LandXML export path requires a live
/// interactive document context and is not exposed through the read-only workflow layer of this
/// platform, so this implementation honestly reports a structured not-supported result with the
/// reason instead of inventing export behavior. A future Autodesk-backed exporter assembly
/// (referencing the conditional AeccDbMgd assemblies like the domain data sources) can swap in
/// behind this same interface; the workflow depends only on ILandXmlExporter, so substitution is
/// transparent.
/// </summary>
public sealed class Civil3DLandXmlExporter : ILandXmlExporter
{
    private readonly ILogger<Civil3DLandXmlExporter> _logger;

    /// <summary>Creates the exporter.</summary>
    /// <param name="logger">The workflow logger for the not-supported notice.</param>
    public Civil3DLandXmlExporter(ILogger<Civil3DLandXmlExporter> logger)
        => _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public LandXmlExportResult Export(LandXmlExportData data)
    {
        _logger.LogInformation(
            "LandXML export requested to {OutputPath} but is not supported by the current "
            + "workflow layer; no file was written.",
            data.OutputPath);

        return new LandXmlExportResult
        {
            Status = LandXmlExportStatus.NotSupported,
            Reason = "LandXML export requires a live interactive Civil 3D document context and "
                + "is not exposed by the read-only workflow layer in this phase; no file was "
                + "written. Use the interactive export command or a future Autodesk-backed exporter.",
            OutputPath = data.OutputPath,
            FileSizeBytes = 0,
            ExportedObjects = [],
            SkippedObjects = [],
            CompletedAtUtc = null,
        };
    }
}
