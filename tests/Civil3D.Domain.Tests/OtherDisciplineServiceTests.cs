using Civil3D.Domain.Cogo.Dtos;
using Civil3D.Domain.Cogo.Repositories;
using Civil3D.Domain.Cogo.Services;
using Civil3D.Domain.Errors;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Pipes.Repositories;
using Civil3D.Domain.Pipes.Services;
using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Domain.Surfaces.Repositories;
using Civil3D.Domain.Surfaces.Services;
using Xunit;
using static Civil3D.Domain.Tests.TestDoubles;

namespace Civil3D.Domain.Tests;

/// <summary>
/// Service behavior for the remaining disciplines: <c>EntityNotFound</c> from the repository
/// becomes a null business result while other domain errors propagate untouched.
/// </summary>
public class OtherDisciplineServiceTests
{
    [Fact]
    public void SurfaceService_MissingSurface_ReturnsNull()
    {
        var service = new SurfaceService(new SurfaceRepository(
            new FakeSurfaceDataSource(new SurfaceCollection([Surface(1, "EG")]))));

        Assert.Null(service.GetByName("missing"));
        Assert.Null(service.GetById(999));
    }

    [Fact]
    public void SurfaceService_FoundSurface_ReturnsDto()
    {
        var service = new SurfaceService(new SurfaceRepository(
            new FakeSurfaceDataSource(new SurfaceCollection([Surface(1, "EG")]))));

        Assert.Equal(1, service.GetByName("eg")!.Id);
    }

    [Fact]
    public void SurfaceService_GetAll_PassesThrough()
    {
        var service = new SurfaceService(new SurfaceRepository(
            new FakeSurfaceDataSource(new SurfaceCollection([Surface(1, "EG")]))));

        Assert.Equal(1, service.GetAll().Count);
        Assert.True(service.Exists("eg"));
        Assert.Equal(1, service.Count());
    }

    [Fact]
    public void SurfaceService_NoActiveDocument_Propagates()
    {
        var service = new SurfaceService(new SurfaceRepository(
            new FakeSurfaceDataSource(_ => throw new DomainException(
                DomainErrorCode.NoActiveDocument, "No drawing open."))));

        DomainException ex = Assert.Throws<DomainException>(() => service.GetAll());

        Assert.Equal(DomainErrorCode.NoActiveDocument, ex.Code);
    }

    [Fact]
    public void PipeService_MissingNetwork_ReturnsNull()
    {
        var service = new PipeService(new PipeRepository(
            new FakePipeDataSource(new PipeNetworkCollection([PipeNetwork(1, "Storm")]))));

        Assert.Null(service.GetByName("sanitary"));
        Assert.Null(service.GetById(42));
    }

    [Fact]
    public void CogoService_MissingPoint_ReturnsNull()
    {
        var service = new CogoService(new CogoRepository(
            new FakeCogoDataSource(new CogoPointCollection([CogoPoint(1, 100)]))));

        Assert.Null(service.GetByPointNumber(777));
        Assert.Null(service.GetById(777));
        Assert.Equal(1, service.GetByPointNumber(100)!.Id);
    }
}
