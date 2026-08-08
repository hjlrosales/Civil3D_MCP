using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Alignments.Repositories;
using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Domain.Surfaces.Repositories;
using Xunit;
using static Civil3D.Domain.Tests.TestDoubles;

namespace Civil3D.Domain.Tests;

/// <summary>
/// The <c>ExistsName</c> extension added for the rename flow: case-insensitive name checks with
/// optional id exclusion, on the real repository classes over in-memory data sources.
/// </summary>
public class RenameRepositoryTests
{
    private static readonly AlignmentCollection Alignments = new(
    [
        Alignment(1, "Mainline"),
        Alignment(2, "Ramp A"),
    ]);

    private static readonly SurfaceCollection Surfaces = new(
    [
        Surface(10, "EG"),
        Surface(20, "FG"),
    ]);

    [Fact]
    public void Alignment_ExistsName_IsCaseInsensitive()
    {
        var repository = new AlignmentRepository(new FakeAlignmentDataSource(Alignments));

        Assert.True(repository.ExistsName("MAINLINE"));
        Assert.False(repository.ExistsName("missing"));
    }

    [Fact]
    public void Alignment_ExistsName_ExcludesTheRenamedId()
    {
        var repository = new AlignmentRepository(new FakeAlignmentDataSource(Alignments));

        // Renaming alignment 1 to its own (existing) name is allowed once id 1 is excluded.
        Assert.False(repository.ExistsName("Mainline", exceptId: 1));
        Assert.True(repository.ExistsName("Ramp A", exceptId: 1));
    }

    [Fact]
    public void Surface_ExistsName_IsCaseInsensitive_AndExcludesId()
    {
        var repository = new SurfaceRepository(new FakeSurfaceDataSource(Surfaces));

        Assert.True(repository.ExistsName("eg"));
        Assert.False(repository.ExistsName("EG", exceptId: 10));
        Assert.True(repository.ExistsName("FG", exceptId: 10));
    }
}
