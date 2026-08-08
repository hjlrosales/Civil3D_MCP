using System.Xml;
using System.Xml.Linq;

namespace Civil3D.Tools.Export.Analysis;

/// <summary>
/// Validates an exported LandXML file after the export: existence, non-zero size and basic XML
/// well-formedness (a full parse). This is the only file-system-touching helper in the analysis
/// folder; it is deliberately separated from the pure analyzer so it can be tested with temp
/// files.
/// </summary>
public static class LandXmlOutputValidator
{
    /// <summary>Validates the file at the given path.</summary>
    /// <param name="path">The expected output file path.</param>
    public static LandXmlOutputValidationResult Validate(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new LandXmlOutputValidationResult();
        }

        var info = new FileInfo(path);
        bool wellFormed = TryLoadWellFormed(path);
        return new LandXmlOutputValidationResult
        {
            Exists = true,
            FileSizeBytes = info.Length,
            IsWellFormedXml = wellFormed,
        };
    }

    private static bool TryLoadWellFormed(string path)
    {
        try
        {
            _ = XDocument.Load(path, LoadOptions.None);
            return true;
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
