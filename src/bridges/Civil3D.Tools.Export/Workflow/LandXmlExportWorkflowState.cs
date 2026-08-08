using Civil3D.Tools.Export.Abstractions;
using Civil3D.Tools.Export.Analysis;
using Civil3D.Tools.Export.Dtos;

namespace Civil3D.Tools.Export.Workflow;

/// <summary>
/// Mutable working state shared between the steps of one LandXML export execution. A fresh
/// instance is created with every workflow; it is never reused across runs, so the mutable
/// fields are safe. Steps write materialized DTOs here; the report step composes them.
/// </summary>
public sealed class LandXmlExportWorkflowState
{
    /// <summary>The request driving this execution.</summary>
    public LandXmlExportRequest Request { get; set; } = new();

    /// <summary>Object counts collected once from the enabled domain services.</summary>
    public int AlignmentCount { get; set; }

    /// <summary>Object counts collected once from the enabled domain services.</summary>
    public int ProfileCount { get; set; }

    /// <summary>Object counts collected once from the enabled domain services.</summary>
    public int SurfaceCount { get; set; }

    /// <summary>Object counts collected once from the enabled domain services.</summary>
    public int CorridorCount { get; set; }

    /// <summary>Object counts collected once from the enabled domain services.</summary>
    public int PipeNetworkCount { get; set; }

    /// <summary>The exporter input composed by the build-options step.</summary>
    public LandXmlExportData? ExportData { get; set; }

    /// <summary>The raw exporter output from the execute step.</summary>
    public LandXmlExportResult? Result { get; set; }

    /// <summary>The output validation from the validate step (null when not supported).</summary>
    public LandXmlOutputValidationResult? Validation { get; set; }

    /// <summary>The analyzer output from the analyze/report steps.</summary>
    public LandXmlAnalysisResult? Analysis { get; set; }

    /// <summary>The composed report; produced by the report step.</summary>
    public LandXmlExportReport? Report { get; set; }
}
