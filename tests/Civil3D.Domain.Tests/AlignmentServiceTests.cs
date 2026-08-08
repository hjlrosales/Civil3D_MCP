using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Alignments.Repositories;
using Civil3D.Domain.Alignments.Services;
using Civil3D.Domain.Errors;
using Civil3D.Domain.Query;
using Xunit;
using static Civil3D.Domain.Tests.TestDoubles;

namespace Civil3D.Domain.Tests;

/// <summary>
/// Alignment service behavior over a mocked repository: business-result translation, in
/// particular <c>EntityNotFound</c> becoming a null result while other errors propagate.
/// </summary>
public class AlignmentServiceTests
{
    private sealed class FakeRepository : IAlignmentRepository
    {
        private readonly AlignmentCollection _items;

        public FakeRepository(AlignmentCollection items) => _items = items;

        public AlignmentCollection GetAll() => _items;

        public AlignmentInfo GetByName(string name)
            => _items.Items.FirstOrDefault(a =>
                string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase))
               ?? throw new DomainException(DomainErrorCode.EntityNotFound, "not found");

        public AlignmentInfo GetById(long id)
            => _items.Items.FirstOrDefault(a => a.Id == id)
               ?? throw new DomainException(DomainErrorCode.EntityNotFound, "not found");

        public bool Exists(string name)
            => _items.Items.Any(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));

        public bool ExistsName(string name, long? exceptId = null)
            => _items.Items.Any(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)
                && (exceptId is null || a.Id != exceptId));

        public int Count() => _items.Count;

        public PageResult<AlignmentInfo> Query(QueryRequest request) => QueryEngine.Apply(_items.Items, request);
    }

    private sealed class NoDocumentRepository : IAlignmentRepository
    {
        public AlignmentCollection GetAll()
            => throw new DomainException(DomainErrorCode.NoActiveDocument, "no doc");

        public AlignmentInfo GetByName(string name) => throw new NotSupportedException();
        public AlignmentInfo GetById(long id) => throw new NotSupportedException();
        public bool Exists(string name) => throw new NotSupportedException();
        public bool ExistsName(string name, long? exceptId = null) => throw new NotSupportedException();
        public int Count() => throw new NotSupportedException();
        public PageResult<AlignmentInfo> Query(QueryRequest request)
            => throw new DomainException(DomainErrorCode.NoActiveDocument, "no doc");
    }

    private static readonly AlignmentCollection Sample = new(
    [
        Alignment(1, "Mainline"),
        Alignment(2, "Ramp A"),
    ]);

    [Fact]
    public void GetAll_ReturnsCollection()
    {
        var service = new AlignmentService(new FakeRepository(Sample));

        AlignmentCollection result = service.GetAll();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetByName_ReturnsNull_WhenMissing()
    {
        var service = new AlignmentService(new FakeRepository(Sample));

        Assert.Null(service.GetByName("missing"));
    }

    [Fact]
    public void GetByName_ReturnsEntity_WhenPresent()
    {
        var service = new AlignmentService(new FakeRepository(Sample));

        AlignmentInfo? result = service.GetByName("mainline");

        Assert.NotNull(result);
        Assert.Equal(1, result!.Id);
    }

    [Fact]
    public void GetById_ReturnsNull_WhenMissing()
    {
        var service = new AlignmentService(new FakeRepository(Sample));

        Assert.Null(service.GetById(99));
    }

    [Fact]
    public void Exists_And_Count_DelegateToRepository()
    {
        var service = new AlignmentService(new FakeRepository(Sample));

        Assert.True(service.Exists("Ramp a"));
        Assert.Equal(2, service.Count());
    }

    [Fact]
    public void NoActiveDocument_Propagates()
    {
        var service = new AlignmentService(new NoDocumentRepository());

        DomainException ex = Assert.Throws<DomainException>(() => service.GetAll());

        Assert.Equal(DomainErrorCode.NoActiveDocument, ex.Code);
    }
}
