using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Alignments.Repositories;
using Civil3D.Domain.Errors;
using Xunit;
using static Civil3D.Domain.Tests.TestDoubles;

namespace Civil3D.Domain.Tests;

/// <summary>
/// Alignment repository behavior over a mocked (in-memory) data source: DTO mapping, lookups,
/// not-found and failure translation. The data source is the mock of the Autodesk API.
/// </summary>
public class AlignmentRepositoryTests
{
    private static readonly AlignmentCollection Sample = new(
    [
        Alignment(1, "Mainline"),
        Alignment(2, "Ramp A"),
        Alignment(3, "Mainline Offset"),
    ]);

    [Fact]
    public void GetAll_ReturnsDtoCollectionFromDataSource()
    {
        var repository = new AlignmentRepository(new FakeAlignmentDataSource(Sample));

        AlignmentCollection result = repository.GetAll();

        Assert.Equal(3, result.Count);
        Assert.Equal("Mainline", result.Items[0].Name);
        Assert.Equal(1, result.Items[0].Id);
        Assert.Equal(1000, result.Items[0].Length);
    }

    [Fact]
    public void GetByName_IsCaseInsensitive()
    {
        var repository = new AlignmentRepository(new FakeAlignmentDataSource(Sample));

        AlignmentInfo result = repository.GetByName("mainline");

        Assert.Equal(1, result.Id);
    }

    [Fact]
    public void GetByName_ThrowsEntityNotFound_WhenMissing()
    {
        var repository = new AlignmentRepository(new FakeAlignmentDataSource(Sample));

        DomainException ex = Assert.Throws<DomainException>(() => repository.GetByName("nope"));

        Assert.Equal(DomainErrorCode.EntityNotFound, ex.Code);
    }

    [Fact]
    public void GetById_ReturnsMatchingAlignment()
    {
        var repository = new AlignmentRepository(new FakeAlignmentDataSource(Sample));

        AlignmentInfo result = repository.GetById(2);

        Assert.Equal("Ramp A", result.Name);
    }

    [Fact]
    public void GetById_ThrowsEntityNotFound_WhenMissing()
    {
        var repository = new AlignmentRepository(new FakeAlignmentDataSource(Sample));

        DomainException ex = Assert.Throws<DomainException>(() => repository.GetById(99));

        Assert.Equal(DomainErrorCode.EntityNotFound, ex.Code);
    }

    [Fact]
    public void Exists_ReturnsTrueForPresentName()
    {
        var repository = new AlignmentRepository(new FakeAlignmentDataSource(Sample));

        Assert.True(repository.Exists("RAMP a"));
        Assert.False(repository.Exists("missing"));
    }

    [Fact]
    public void Count_ReturnsItemCount()
    {
        var repository = new AlignmentRepository(new FakeAlignmentDataSource(Sample));

        Assert.Equal(3, repository.Count());
    }

    [Fact]
    public void NoActiveDocument_PassesThrough()
    {
        var repository = new AlignmentRepository(
            new FakeAlignmentDataSource(_ => throw new DomainException(DomainErrorCode.NoActiveDocument, "no doc")));

        DomainException ex = Assert.Throws<DomainException>(() => repository.GetAll());

        Assert.Equal(DomainErrorCode.NoActiveDocument, ex.Code);
    }

    [Fact]
    public void UnexpectedDataSourceFailure_MapsToInternal()
    {
        var repository = new AlignmentRepository(
            new FakeAlignmentDataSource(_ => throw new InvalidOperationException("boom")));

        DomainException ex = Assert.Throws<DomainException>(() => repository.GetAll());

        Assert.Equal(DomainErrorCode.Internal, ex.Code);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void DataSourceCancellation_PropagatesAsOperationCanceled()
    {
        // The repository API carries no cancellation token (by design); a cancelled read inside
        // the data source still surfaces as OperationCanceledException rather than being remapped.
        var repository = new AlignmentRepository(
            new FakeAlignmentDataSource(_ => throw new OperationCanceledException()));

        Assert.Throws<OperationCanceledException>(() => repository.GetAll());
    }
}
