using Civil3D.Domain.Pipes.Materials;
using Xunit;

namespace Civil3D.Tools.Editing.Tests;

/// <summary>
/// The <see cref="PipeMaterials"/> catalog: name/alias resolution, catalog family-description
/// variants (RCP resolving to the Concrete family), the generic fallback for unknown materials,
/// and the standard SDR / PN rating sets per material.
/// </summary>
public class PipeMaterialsTests
{
    [Theory]
    [InlineData("HDPE", "HDPE")]
    [InlineData("hdpe", "HDPE")]
    [InlineData("PVC", "PVC")]
    [InlineData("Ductile Iron", "Ductile Iron")]
    [InlineData("DI", "Ductile Iron")]
    [InlineData("RCP", "Concrete")]
    [InlineData("Reinforced Concrete", "Concrete")]
    [InlineData("Corrugated HDPE", "Corrugated HDPE")]
    [InlineData("CMP", "Corrugated Metal")]
    public void Resolve_RecognizesMaterialNamesAndAliases(string input, string canonical)
    {
        PipeMaterialInfo? info = PipeMaterials.Resolve(input);

        Assert.NotNull(info);
        Assert.Equal(canonical, info!.Name);
    }

    [Fact]
    public void Resolve_UnknownMaterial_ReturnsNull()
    {
        Assert.Null(PipeMaterials.Resolve("Polypropylene"));
        Assert.Null(PipeMaterials.Resolve(null));
        Assert.Null(PipeMaterials.Resolve("  "));
    }

    [Theory]
    [InlineData("HDPE", "HDPE Pipe SI")]
    [InlineData("PVC", "PVC Pipe SI")]
    [InlineData("Ductile Iron", "Ductile Iron Pipe SI")]
    [InlineData("RCP", "Concrete Pipe SI")]
    [InlineData("Corrugated Metal", "Corrugated Metal Pipe SI")]
    public void CatalogVariants_KnownMaterial_StartsWithMetricFamily(string material, string firstVariant)
    {
        Assert.Equal(firstVariant, PipeMaterials.CatalogVariants(material)[0]);
    }

    [Fact]
    public void CatalogVariants_Rcp_IncludesReinforcedConcreteFallbacks()
    {
        IReadOnlyList<string> variants = PipeMaterials.CatalogVariants("RCP");

        Assert.Contains("Reinforced Concrete Pipe SI", variants);
        Assert.Contains("Reinforced Concrete Pipe", variants);
    }

    [Fact]
    public void CatalogVariants_UnknownMaterial_UsesGenericFallback()
    {
        IReadOnlyList<string> variants = PipeMaterials.CatalogVariants("Polypropylene");

        Assert.Equal(new[] { "Polypropylene Pipe SI", "Polypropylene Pipe" }, variants);
    }

    [Fact]
    public void Hdpe_RatedBySdrAndPn_WithStandardValues()
    {
        PipeMaterialInfo hdpe = PipeMaterials.Resolve("HDPE")!;

        Assert.Equal(PipeRatingMode.SdrAndPn, hdpe.RatingMode);
        Assert.Equal(new double[] { 11, 17, 26, 32.5 }, hdpe.SupportedSdrValues);
        Assert.Equal(new double[] { 6, 8, 10, 16 }, hdpe.SupportedPressureClassesBar);
    }

    [Fact]
    public void Pvc_RatedBySdrAndPn_WithStandardValues()
    {
        PipeMaterialInfo pvc = PipeMaterials.Resolve("PVC")!;

        Assert.Equal(PipeRatingMode.SdrAndPn, pvc.RatingMode);
        Assert.Equal(new double[] { 26, 35, 41 }, pvc.SupportedSdrValues);
        Assert.Equal(new double[] { 6, 10, 12.5, 16 }, pvc.SupportedPressureClassesBar);
    }

    [Fact]
    public void DuctileIron_RatedByPressureClassOnly_NoSdr()
    {
        PipeMaterialInfo di = PipeMaterials.Resolve("Ductile Iron")!;

        Assert.Equal(PipeRatingMode.PressureClassOnly, di.RatingMode);
        Assert.Empty(di.SupportedSdrValues);
        Assert.Equal(new double[] { 10, 16, 25, 40 }, di.SupportedPressureClassesBar);
    }

    [Fact]
    public void ConcreteRcp_NoSdrOrPnRating()
    {
        PipeMaterialInfo rcp = PipeMaterials.Resolve("RCP")!;

        Assert.Equal(PipeRatingMode.None, rcp.RatingMode);
        Assert.Empty(rcp.SupportedSdrValues);
        Assert.Empty(rcp.SupportedPressureClassesBar);
    }
}
