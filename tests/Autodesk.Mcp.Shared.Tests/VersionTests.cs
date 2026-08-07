using Autodesk.Mcp.Shared.Contracts;
using Xunit;

namespace Autodesk.Mcp.Shared.Tests;

/// <summary>Semantic version parsing, formatting and precedence.</summary>
public class VersionTests
{
    [Theory]
    [InlineData("1.2.3", 1, 2, 3, "", "")]
    [InlineData("1.2.3-beta.1+build.5", 1, 2, 3, "beta.1", "build.5")]
    [InlineData("0.0.0", 0, 0, 0, "", "")]
    [InlineData("1.2", 1, 2, 0, "", "")]
    [InlineData("10.20.30-rc", 10, 20, 30, "rc", "")]
    public void Parse_ProducesExpectedComponents(string text, int major, int minor, int patch, string pre, string build)
    {
        var version = VersionInformation.Parse(text);

        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(patch, version.Patch);
        Assert.Equal(pre, version.PreRelease);
        Assert.Equal(build, version.BuildMetadata);
        Assert.Equal(pre.Length > 0, version.IsPreRelease);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("v1.2.3")]
    [InlineData("a.b.c")]
    [InlineData("1.2.3.4")]
    [InlineData("1.-2.3")]
    [InlineData("1.2.3-alpha..1")]
    [InlineData("1.2.3-")]
    [InlineData("1.2.3 alpha")]
    public void TryParse_RejectsInvalidInput(string? value)
        => Assert.False(VersionInformation.TryParse(value, out _));

    [Fact]
    public void Precedence_FollowsSemVerSpec()
    {
        var versions = new[]
        {
            VersionInformation.Parse("1.0.0-alpha"),
            VersionInformation.Parse("1.0.0-alpha.1"),
            VersionInformation.Parse("1.0.0-alpha.beta"),
            VersionInformation.Parse("1.0.0-beta"),
            VersionInformation.Parse("1.0.0-beta.2"),
            VersionInformation.Parse("1.0.0-beta.11"),
            VersionInformation.Parse("1.0.0-rc.1"),
            VersionInformation.Parse("1.0.0"),
        };

        for (int i = 1; i < versions.Length; i++)
        {
            Assert.True(versions[i - 1] < versions[i], $"{versions[i - 1]} should precede {versions[i]}");
        }
    }

    [Fact]
    public void BuildMetadata_IsIgnoredForPrecedence()
    {
        Assert.Equal(0, VersionInformation.Parse("1.0.0").CompareTo(VersionInformation.Parse("1.0.0+build1")));
        Assert.Equal(0, VersionInformation.Parse("1.0.0+b1").CompareTo(VersionInformation.Parse("1.0.0+b2")));
    }

    [Fact]
    public void StructuralEquality_IncludesBuildMetadata()
    {
        Assert.Equal(VersionInformation.Parse("1.0.0+b1"), VersionInformation.Parse("1.0.0+b1"));
        Assert.NotEqual(VersionInformation.Parse("1.0.0+b1"), VersionInformation.Parse("1.0.0+b2"));
    }

    [Fact]
    public void ComparisonOperators_Work()
    {
        var low = VersionInformation.Parse("1.2.0");
        var high = VersionInformation.Parse("1.2.1");

        Assert.True(low < high);
        Assert.True(high > low);
        Assert.True(low <= high);
        Assert.True(high >= low);
        Assert.True(low <= VersionInformation.Parse("1.2.0"));
    }

    [Fact]
    public void ToString_RendersCanonicalForm()
    {
        Assert.Equal("1.2.3", new VersionInformation(1, 2, 3).ToString());
        Assert.Equal("1.2.3-beta.1+build.5", new VersionInformation(1, 2, 3, "beta.1", "build.5").ToString());
    }

    [Fact]
    public void InvalidConstructorInput_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new VersionInformation(-1, 0, 0));
        Assert.Throws<ArgumentException>(() => new VersionInformation(1, 0, 0, "bad value"));
    }
}
