using System.Text.Json;
using Xunit;

namespace Civil3D.Domain.Query.Tests;

/// <summary>The query DTOs round-trip through System.Text.Json (wire safety).</summary>
public class SerializationTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void QueryRequest_RoundTripsWithFiltersAndSorts()
    {
        var source = new QueryRequest
        {
            Filters =
            [
                new FilterExpression { Field = "Name", Operator = FilterOperator.Contains, Value = "main" },
                new FilterExpression { Field = "Kind", Operator = FilterOperator.In, Values = ["Road", "Rail"] },
            ],
            Sorts = [new SortExpression { Field = "Length", Direction = SortDirection.Descending }],
            Page = new PageRequest { Page = 2, PageSize = 25 },
        };

        string json = JsonSerializer.Serialize(source, Options);
        QueryRequest? result = JsonSerializer.Deserialize<QueryRequest>(json, Options);

        Assert.NotNull(result);
        Assert.Equal(2, result.Filters!.Count);
        Assert.Equal(FilterOperator.In, result.Filters[1].Operator);
        Assert.Equal(SortDirection.Descending, result.Sorts![0].Direction);
        Assert.Equal(2, result.Page!.Page);
        Assert.Equal(25, result.Page.PageSize);
    }

    [Fact]
    public void FilterExpression_JsonValues_BindAsJsonElement()
    {
        var source = new FilterExpression { Field = "Length", Operator = FilterOperator.GreaterThan, Value = 42 };

        string json = JsonSerializer.Serialize(source, Options);
        FilterExpression? result = JsonSerializer.Deserialize<FilterExpression>(json, Options);

        Assert.NotNull(result);
        // The bound value is a JsonElement; the engine normalizes it during matching.
        Assert.True(QueryEngine.Filter(
            [new Widget(1, "A", null, 100, WidgetKind.Road, 1)],
            [result!]).Count == 1);
    }

    [Fact]
    public void PageResult_RoundTripsWithMetadata()
    {
        var source = new PageResult<Widget>(
            [new Widget(1, "A", null, 10, WidgetKind.Road, 1)], 1, 25, 120)
        {
            Statistics = new QueryStatistics(120, 1, 3),
        };

        string json = JsonSerializer.Serialize(source, Options);
        PageResult<Widget>? result = JsonSerializer.Deserialize<PageResult<Widget>>(json, Options);

        Assert.NotNull(result);
        Assert.Equal(120, result.TotalCount);
        Assert.Equal(5, result.TotalPages);
        Assert.True(result.HasNextPage);
        Assert.Equal(120, result.Statistics.MatchedCount);
        Assert.Equal(1, Assert.Single(result.Items).Id);
    }

    [Fact]
    public void SearchRequest_RoundTrips()
    {
        var source = new SearchRequest
        {
            Query = "main",
            Kinds = ["alignment", "surface"],
            Page = new PageRequest { Page = 1, PageSize = 10 },
        };

        string json = JsonSerializer.Serialize(source, Options);
        SearchRequest? result = JsonSerializer.Deserialize<SearchRequest>(json, Options);

        Assert.NotNull(result);
        Assert.Equal("main", result.Query);
        Assert.Equal(["alignment", "surface"], result.Kinds);
    }

    [Fact]
    public void DefaultQueryRequest_IsUsable()
    {
        var request = new QueryRequest();

        string json = JsonSerializer.Serialize(request, Options);
        QueryRequest? result = JsonSerializer.Deserialize<QueryRequest>(json, Options);

        Assert.NotNull(result);
        Assert.Null(result.Filters);
        Assert.Null(result.Sorts);
        Assert.Null(result.Page);
    }
}
