using Xunit;

namespace Civil3D.Domain.Query.Tests;

/// <summary>Paging math, clamping and out-of-range behavior.</summary>
public class PaginationTests
{
    private static IReadOnlyList<Widget> Items(int count)
        => Enumerable.Range(1, count).Select(i => new Widget(i, $"W{i}", null, i, WidgetKind.Road, i)).ToArray();

    [Fact]
    public void PageOne_ReturnsFirstPageSizeItems()
    {
        PageResult<Widget> result = QueryEngine.Apply(Items(10), new QueryRequest
        {
            Page = new PageRequest { Page = 1, PageSize = 4 },
        });

        Assert.Equal([1L, 2L, 3L, 4L], result.Items.Select(w => w.Id));
        Assert.Equal(10, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.True(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
        Assert.Equal(10, result.Statistics.MatchedCount);
        Assert.Equal(4, result.Statistics.ReturnedCount);
    }

    [Fact]
    public void MiddlePage_ReturnsCorrectSlice()
    {
        PageResult<Widget> result = QueryEngine.Apply(Items(10), new QueryRequest
        {
            Page = new PageRequest { Page = 2, PageSize = 4 },
        });

        Assert.Equal([5L, 6L, 7L, 8L], result.Items.Select(w => w.Id));
        Assert.True(result.HasNextPage);
        Assert.True(result.HasPreviousPage);
    }

    [Fact]
    public void LastPage_ReturnsRemainder()
    {
        PageResult<Widget> result = QueryEngine.Apply(Items(10), new QueryRequest
        {
            Page = new PageRequest { Page = 3, PageSize = 4 },
        });

        Assert.Equal([9L, 10L], result.Items.Select(w => w.Id));
        Assert.False(result.HasNextPage);
    }

    [Fact]
    public void OutOfRangePage_ReturnsEmptyPage()
    {
        PageResult<Widget> result = QueryEngine.Apply(Items(4), new QueryRequest
        {
            Page = new PageRequest { Page = 9, PageSize = 4 },
        });

        Assert.Empty(result.Items);
        Assert.Equal(4, result.TotalCount);
        Assert.False(result.HasNextPage);
    }

    [Fact]
    public void NullRequest_UsesDefaultPaging()
    {
        PageResult<Widget> result = QueryEngine.Apply(Items(120), null);

        Assert.Equal(50, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(50, result.PageSize);
    }

    [Fact]
    public void PageSize_IsClampedToMaximum()
    {
        PageResult<Widget> result = QueryEngine.Apply(Items(5), new QueryRequest
        {
            Page = new PageRequest { Page = 1, PageSize = 10_000 },
        });

        Assert.Equal(500, result.PageSize);
    }

    [Fact]
    public void PageNumber_IsClampedToOne()
    {
        PageResult<Widget> result = QueryEngine.Apply(Items(5), new QueryRequest
        {
            Page = new PageRequest { Page = 0, PageSize = 2 },
        });

        Assert.Equal(1, result.Page);
        Assert.Equal([1L, 2L], result.Items.Select(w => w.Id));
    }

    [Fact]
    public void Apply_CombinesFilterSortAndPage()
    {
        PageResult<Widget> result = QueryEngine.Apply(Items(10), new QueryRequest
        {
            Filters = [new FilterExpression { Field = "Name", Operator = FilterOperator.Contains, Value = "W" }],
            Sorts = [new SortExpression { Field = "Id", Direction = SortDirection.Descending }],
            Page = new PageRequest { Page = 1, PageSize = 3 },
        });

        Assert.Equal([10L, 9L, 8L], result.Items.Select(w => w.Id));
        Assert.Equal(10, result.TotalCount);
    }
}
