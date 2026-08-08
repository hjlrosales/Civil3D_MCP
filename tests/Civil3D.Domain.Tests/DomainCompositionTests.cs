using Civil3D.Domain.Alignments.Repositories;
using Civil3D.Domain.Alignments.Services;
using Civil3D.Domain.Cogo.Repositories;
using Civil3D.Domain.Cogo.Services;
using Civil3D.Domain.Corridors.Repositories;
using Civil3D.Domain.Corridors.Services;
using Civil3D.Domain.Pipes.Repositories;
using Civil3D.Domain.Pipes.Services;
using Civil3D.Domain.Profiles.Repositories;
using Civil3D.Domain.Profiles.Services;
using Civil3D.Domain.Styles.Dtos;
using Civil3D.Domain.Styles.Repositories;
using Civil3D.Domain.Styles.Services;
using Civil3D.Domain.Surfaces.Repositories;
using Civil3D.Domain.Surfaces.Services;
using Xunit;
using static Civil3D.Domain.Tests.TestDoubles;

namespace Civil3D.Domain.Tests;

/// <summary>
/// Constructor-injection wiring: every discipline's service can be constructed with its
/// repository, which is constructed with its data source, and a query flows end to end. The
/// bridge-level <c>AddCivil3DBridge</c> registration of these services is exercised by the
/// Civil3D.Tools.Drawing discovery tests.
/// </summary>
public class DomainCompositionTests
{
    [Fact]
    public void AlignmentChain_ReturnsData()
    {
        var service = new AlignmentService(new AlignmentRepository(
            new FakeAlignmentDataSource(AlignmentCollection(Alignment(1, "A")))));

        Assert.Equal(1, service.GetAll().Count);
    }

    [Fact]
    public void SurfaceChain_ReturnsData()
    {
        var service = new SurfaceService(new SurfaceRepository(
            new FakeSurfaceDataSource(SurfaceCollection(Surface(1, "EG")))));

        Assert.Equal(1, service.GetAll().Count);
    }

    [Fact]
    public void ProfileChain_ReturnsData()
    {
        var service = new ProfileService(new ProfileRepository(
            new FakeProfileDataSource(ProfileCollection(Profile(1, "EG", 7)))));

        Assert.Equal(1, service.GetAll().Count);
    }

    [Fact]
    public void CorridorChain_ReturnsData()
    {
        var service = new CorridorService(new CorridorRepository(
            new FakeCorridorDataSource(CorridorCollection(Corridor(1, "C", 7)))));

        Assert.Equal(1, service.GetAll().Count);
    }

    [Fact]
    public void PipeChain_ReturnsData()
    {
        var service = new PipeService(new PipeRepository(
            new FakePipeDataSource(PipeNetworkCollection(PipeNetwork(1, "Storm")))));

        Assert.Equal(1, service.GetAll().Count);
    }

    [Fact]
    public void CogoChain_ReturnsData()
    {
        var service = new CogoService(new CogoRepository(
            new FakeCogoDataSource(CogoPointCollection(CogoPoint(1, 100)))));

        Assert.Equal(1, service.GetAll().Count);
    }

    [Fact]
    public void StyleChain_ReturnsData()
    {
        var service = new StyleService(new StyleRepository(
            new FakeStyleDataSource(StyleCollection(Style(1, "S", StyleKind.Alignment)))));

        Assert.Equal(1, service.GetAll().Count);
    }
}
