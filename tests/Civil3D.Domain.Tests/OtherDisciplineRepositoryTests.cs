using Civil3D.Domain.Cogo.Dtos;
using Civil3D.Domain.Cogo.Repositories;
using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Domain.Corridors.Repositories;
using Civil3D.Domain.Errors;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Pipes.Repositories;
using Civil3D.Domain.Profiles.Dtos;
using Civil3D.Domain.Profiles.Repositories;
using Civil3D.Domain.Styles.Dtos;
using Civil3D.Domain.Styles.Repositories;
using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Domain.Surfaces.Repositories;
using Xunit;
using static Civil3D.Domain.Tests.TestDoubles;

namespace Civil3D.Domain.Tests;

/// <summary>
/// Repository behavior for the remaining disciplines (surfaces, profiles, corridors, pipes, COGO
/// points, styles) over mocked in-memory data sources: DTO mapping, case-insensitive lookups,
/// not-found and failure translation.
/// </summary>
public class OtherDisciplineRepositoryTests
{
    [Fact]
    public void SurfaceRepository_GetAll_MapsDataSourceDtos()
    {
        var repository = new SurfaceRepository(new FakeSurfaceDataSource(
            new SurfaceCollection([Surface(1, "EG"), Surface(2, "FG")])));

        var result = repository.GetAll();

        Assert.Equal(2, result.Count);
        Assert.Equal("EG", result.Items[0].Name);
        Assert.Equal(SurfaceKind.Tin, result.Items[1].Kind);
    }

    [Fact]
    public void SurfaceRepository_GetByName_IsCaseInsensitive()
    {
        var repository = new SurfaceRepository(new FakeSurfaceDataSource(
            new SurfaceCollection([Surface(1, "Existing Ground")])));

        var result = repository.GetByName("existing ground");

        Assert.Equal(1, result.Id);
    }

    [Fact]
    public void ProfileRepository_GetAll_And_GetById()
    {
        var repository = new ProfileRepository(new FakeProfileDataSource(
            new ProfileCollection([Profile(1, "EG Profile", 42), Profile(2, "FG Profile", 42)])));

        Assert.Equal(2, repository.GetAll().Count);
        Assert.Equal("FG Profile", repository.GetById(2).Name);
        Assert.Equal(42, repository.GetById(2).AlignmentId);
    }

    [Fact]
    public void CorridorRepository_GetAll_And_Count()
    {
        var repository = new CorridorRepository(new FakeCorridorDataSource(
            new CorridorCollection([Corridor(1, "Main Corridor", 7)])));

        Assert.Equal(1, repository.Count());
        Assert.True(repository.Exists("main corridor"));
        Assert.Equal(1, repository.GetByName("MAIN CORRIDOR").Id);
    }

    [Fact]
    public void PipeRepository_GetAll_PreservesNestedParts()
    {
        var repository = new PipeRepository(new FakePipeDataSource(
            new PipeNetworkCollection([PipeNetwork(1, "Storm")])));

        var network = Assert.Single(repository.GetAll().Items);
        Assert.Equal("Storm", network.Name);
        Assert.Equal(101, Assert.Single(network.Pipes).Id);
        Assert.Equal(201, Assert.Single(network.Structures).Id);
        Assert.Equal(1, repository.GetByName("storm").Id);
    }

    [Fact]
    public void CogoRepository_GetByPointNumber_FindsPoint()
    {
        var repository = new CogoRepository(new FakeCogoDataSource(
            new CogoPointCollection([CogoPoint(1, 100), CogoPoint(2, 200)])));

        var point = repository.GetByPointNumber(200);

        Assert.Equal(2, point.Id);
        Assert.Equal(100.5, point.Easting);
    }

    [Fact]
    public void StyleRepository_GetAll_And_GetByName()
    {
        var repository = new StyleRepository(new FakeStyleDataSource(
            new StyleCollection([Style(1, "Road Design", StyleKind.Alignment)])));

        Assert.Equal(1, repository.Count());
        Assert.Equal(StyleKind.Alignment, repository.GetByName("road design").Kind);
    }

    [Fact]
    public void LookupByMissingName_ThrowsEntityNotFound()
    {
        var repository = new SurfaceRepository(new FakeSurfaceDataSource(
            new SurfaceCollection([Surface(1, "EG")])));

        var ex = Assert.Throws<DomainException>(() => repository.GetByName("nope"));

        Assert.Equal(DomainErrorCode.EntityNotFound, ex.Code);
    }

    [Fact]
    public void DataSourceNoActiveDocument_PropagatesUnchanged()
    {
        var repository = new CorridorRepository(new FakeCorridorDataSource(
            _ => throw new DomainException(DomainErrorCode.NoActiveDocument, "No drawing open.")));

        var ex = Assert.Throws<DomainException>(() => repository.GetAll());

        Assert.Equal(DomainErrorCode.NoActiveDocument, ex.Code);
    }

    [Fact]
    public void UnexpectedDataSourceFailure_MapsToInternal()
    {
        var repository = new StyleRepository(new FakeStyleDataSource(
            _ => throw new InvalidOperationException("Autodesk exploded")));

        var ex = Assert.Throws<DomainException>(() => repository.GetAll());

        Assert.Equal(DomainErrorCode.Internal, ex.Code);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }
}
