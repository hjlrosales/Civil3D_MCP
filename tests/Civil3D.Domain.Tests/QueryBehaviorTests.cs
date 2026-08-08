using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Alignments.Repositories;
using Civil3D.Domain.Alignments.Services;
using Civil3D.Domain.Errors;
using Civil3D.Domain.Query;
using Xunit;
using static Civil3D.Domain.Tests.TestDoubles;

namespace Civil3D.Domain.Tests;

/// <summary>
/// Repository and service <c>Query</c> behavior: the shared query engine applies filters, sorts
/// and paging over the data source's single read; malformed requests surface as
/// <see cref="QueryException"/> (mapped to E_INVALID_PARAMETERS by the tool layer), not as
/// internal errors.
/// </summary>
public class QueryBehaviorTests
{
    private static AlignmentRepository Repository(AlignmentCollection items)
        => new(new FakeAlignmentDataSource(items));

    [Fact]
    public void Repository_Query_AppliesFiltersAndPaging()
    {
        var repository = Repository(new AlignmentCollection(
        [
            Alignment(1, "Mainline"),
            Alignment(2, "Ramp A"),
            Alignment(3, "Mainline Offset"),
        ]));

        PageResult<AlignmentInfo> result = repository.Query(new QueryRequest
        {
            Filters = [new FilterExpression { Field = "Name", Operator = FilterOperator.StartsWith, Value = "Main" }],
            Page = new PageRequest { Page = 1, PageSize = 1 },
        });

        Assert.Equal([1L], result.Items.Select(a => a.Id));
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public void Repository_Query_AppliesSorting()
    {
        var repository = Repository(new AlignmentCollection(
        [
            Alignment(1, "Zulu"),
            Alignment(2, "Alpha"),
            Alignment(3, "Mike"),
        ]));

        PageResult<AlignmentInfo> result = repository.Query(new QueryRequest
        {
            Sorts = [new SortExpression { Field = "Name" }],
            Page = new PageRequest { PageSize = 50 },
        });

        Assert.Equal([2L, 3L, 1L], result.Items.Select(a => a.Id));
    }

    [Fact]
    public void Repository_Query_UnknownField_ThrowsQueryExceptionNotInternal()
    {
        var repository = Repository(new AlignmentCollection([Alignment(1, "A")]));

        var ex = Assert.Throws<QueryException>(() => repository.Query(new QueryRequest
        {
            Filters = [new FilterExpression { Field = "Nope", Operator = FilterOperator.Equals, Value = "x" }],
        }));

        Assert.Contains("Nope", ex.Message);
    }

    [Fact]
    public void Repository_Query_NoActiveDocument_Propagates()
    {
        var repository = new AlignmentRepository(new FakeAlignmentDataSource(
            _ => throw new DomainException(DomainErrorCode.NoActiveDocument, "No drawing open.")));

        DomainException ex = Assert.Throws<DomainException>(() => repository.Query(new QueryRequest()));

        Assert.Equal(DomainErrorCode.NoActiveDocument, ex.Code);
    }

    [Fact]
    public void Service_Query_PassesThroughToRepository()
    {
        var service = new AlignmentService(Repository(new AlignmentCollection(
        [
            Alignment(1, "A"),
            Alignment(2, "B"),
            Alignment(3, "C"),
        ])));

        PageResult<AlignmentInfo> result = service.Query(new QueryRequest
        {
            Page = new PageRequest { Page = 2, PageSize = 2 },
        });

        Assert.Equal([3L], result.Items.Select(a => a.Id));
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.Page);
    }

    [Fact]
    public void Service_Query_DefaultRequest_ReturnsFirstPage()
    {
        var service = new AlignmentService(Repository(new AlignmentCollection(
            Enumerable.Range(1, 120).Select(i => Alignment(i, $"A{i}")).ToArray())));

        PageResult<AlignmentInfo> result = service.Query(new QueryRequest());

        Assert.Equal(50, result.Items.Count);
        Assert.Equal(120, result.TotalCount);
    }
}
