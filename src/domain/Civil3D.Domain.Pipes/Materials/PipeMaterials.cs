namespace Civil3D.Domain.Pipes.Materials;

/// <summary>
/// How a pipe material is rated. Thermoplastic pipes (HDPE, PVC) are rated by both the Standard
/// Dimension Ratio (SDR) and the nominal pressure class (PN); rigid metal pipes (Ductile Iron)
/// are rated by PN pressure class only; gravity and rigid concrete pipes (RCP, corrugated
/// metal) have no SDR/PN rating at all.
/// </summary>
public enum PipeRatingMode
{
    /// <summary>Rated by both Standard Dimension Ratio (SDR) and nominal pressure class (PN).</summary>
    SdrAndPn,

    /// <summary>Rated by nominal pressure class (PN) only; SDR does not apply.</summary>
    PressureClassOnly,

    /// <summary>Neither SDR nor PN applies.</summary>
    None,
}

/// <summary>
/// Domain knowledge about a pipe material: the catalog family-description variants used to add
/// its part family to a parts list, and the SDR values / PN pressure classes (bar) that are
/// standard for it. Autodesk-free; the single source of truth shared by the create-pipe-network
/// repository (family resolution) and the create-pipe service (rating validation).
/// </summary>
public sealed record PipeMaterialInfo(
    string Name,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> CatalogFamilyVariants,
    PipeRatingMode RatingMode,
    IReadOnlyList<double> SupportedSdrValues,
    IReadOnlyList<double> SupportedPressureClassesBar);

/// <summary>
/// The catalog of pipe materials supported by the editing tools. Lookups are case-insensitive
/// and accept both the canonical material name and its aliases (for example "RCP" resolves to
/// the Concrete entry). Unknown materials fall back to a generic "&lt;material&gt; Pipe SI" /
/// "&lt;material&gt; Pipe" family description and skip rating validation, so custom catalogs
/// keep working.
/// </summary>
public static class PipeMaterials
{
    /// <summary>The supported materials and their catalog variants / rating systems.</summary>
    public static readonly IReadOnlyList<PipeMaterialInfo> All =
    [
        new(
            "HDPE",
            Aliases: [],
            CatalogFamilyVariants: ["HDPE Pipe SI", "HDPE Pipe"],
            RatingMode: PipeRatingMode.SdrAndPn,
            SupportedSdrValues: [11, 17, 26, 32.5],
            SupportedPressureClassesBar: [6, 8, 10, 16]),
        new(
            "PVC",
            Aliases: ["PVC-U", "uPVC"],
            CatalogFamilyVariants: ["PVC Pipe SI", "PVC Pipe"],
            RatingMode: PipeRatingMode.SdrAndPn,
            SupportedSdrValues: [26, 35, 41],
            SupportedPressureClassesBar: [6, 10, 12.5, 16]),
        new(
            "Ductile Iron",
            Aliases: ["DI"],
            CatalogFamilyVariants: ["Ductile Iron Pipe SI", "Ductile Iron Pipe"],
            RatingMode: PipeRatingMode.PressureClassOnly,
            SupportedSdrValues: [],
            SupportedPressureClassesBar: [10, 16, 25, 40]),
        new(
            "Concrete",
            Aliases: ["RCP", "Reinforced Concrete"],
            CatalogFamilyVariants:
                ["Concrete Pipe SI", "Reinforced Concrete Pipe SI", "Concrete Pipe", "Reinforced Concrete Pipe"],
            RatingMode: PipeRatingMode.None,
            SupportedSdrValues: [],
            SupportedPressureClassesBar: []),
        new(
            "Corrugated HDPE",
            Aliases: ["CHDPE"],
            CatalogFamilyVariants: ["Corrugated HDPE Pipe SI", "Corrugated HDPE Pipe"],
            RatingMode: PipeRatingMode.None,
            SupportedSdrValues: [],
            SupportedPressureClassesBar: []),
        new(
            "Corrugated Metal",
            Aliases: ["CMP"],
            CatalogFamilyVariants: ["Corrugated Metal Pipe SI", "Corrugated Metal Pipe"],
            RatingMode: PipeRatingMode.None,
            SupportedSdrValues: [],
            SupportedPressureClassesBar: []),
    ];

    private static readonly IReadOnlyDictionary<string, PipeMaterialInfo> ByKey = BuildLookup();

    /// <summary>
    /// Resolves a material name or alias to its catalog entry, or null when unknown.
    /// </summary>
    public static PipeMaterialInfo? Resolve(string? material)
    {
        if (string.IsNullOrWhiteSpace(material))
        {
            return null;
        }

        return ByKey.TryGetValue(material.Trim(), out PipeMaterialInfo? info) ? info : null;
    }

    /// <summary>
    /// Returns the catalog family-description variants to try for a material: the known map
    /// first (aliases such as RCP included), then a generic "&lt;material&gt; Pipe SI" /
    /// "&lt;material&gt; Pipe" fallback so unknown materials still resolve when the catalog
    /// names them that way.
    /// </summary>
    public static IReadOnlyList<string> CatalogVariants(string material)
    {
        string trimmed = material.Trim();
        if (ByKey.TryGetValue(trimmed, out PipeMaterialInfo? info))
        {
            return info.CatalogFamilyVariants;
        }

        return [$"{trimmed} Pipe SI", $"{trimmed} Pipe"];
    }

    private static IReadOnlyDictionary<string, PipeMaterialInfo> BuildLookup()
    {
        var map = new Dictionary<string, PipeMaterialInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (PipeMaterialInfo info in All)
        {
            map[info.Name] = info;
            foreach (string alias in info.Aliases)
            {
                map[alias] = info;
            }
        }

        return map;
    }
}
