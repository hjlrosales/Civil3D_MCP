using Xunit;

namespace Civil3D.Domain.Query.Tests;

/// <summary>Free-text search matching and query validation (unknown fields, malformed operators).</summary>
public class SearchAndValidationTests
{
    private static readonly IReadOnlyList<Widget> Items =
    [
        new(1, "Mainline", "Primary road corridor", 1_000, WidgetKind.Road, 5),
        new(2, "Ramp A", "Curved ramp", 300, WidgetKind.Road, 2),
        new(3, "Rail Spur", null, 2_000, WidgetKind.Rail, null),
    ];

    [Fact]
    public void MatchesSearch_MatchesNameOrDescription()
    {
        Assert.True(QueryEngine.MatchesSearch(Items[0], "mainline", "Name", "Description"));
        Assert.True(QueryEngine.MatchesSearch(Items[0], "corridor", "Name", "Description"));
        Assert.False(QueryEngine.MatchesSearch(Items[2], "ramp", "Name", "Description"));
    }

    [Fact]
    public void MatchesSearch_EmptyQuery_MatchesEverything()
    {
        Assert.True(QueryEngine.MatchesSearch(Items[2], "", "Name"));
    }

    [Fact]
    public void MatchesSearch_WorksOnNumericFieldsViaToString()
    {
        Assert.True(QueryEngine.MatchesSearch(Items[2], "3", "Id"));
    }

    [Fact]
    public void Apply_UnknownFilterField_ThrowsQueryException()
    {
        var request = new QueryRequest
        {
            Filters = [new FilterExpression { Field = "Nope", Operator = FilterOperator.Equals, Value = "x" }],
        };

        Assert.Throws<QueryException>(() => QueryEngine.Apply(Items, request));
    }

    [Fact]
    public void Apply_UnknownSortField_ThrowsQueryException()
    {
        var request = new QueryRequest
        {
            Sorts = [new SortExpression { Field = "Nope" }],
        };

        Assert.Throws<QueryException>(() => QueryEngine.Apply(Items, request));
    }

    [Fact]
    public void Apply_UnknownSelectedField_ThrowsQueryException()
    {
        var request = new QueryRequest
        {
            Fields = new FieldSelection { Fields = ["Nope"] },
        };

        Assert.Throws<QueryException>(() => QueryEngine.Apply(Items, request));
    }

    [Fact]
    public void Apply_InWithoutValues_ThrowsQueryException()
    {
        var request = new QueryRequest
        {
            Filters = [new FilterExpression { Field = "Kind", Operator = FilterOperator.In }],
        };

        Assert.Throws<QueryException>(() => QueryEngine.Apply(Items, request));
    }

    [Fact]
    public void Apply_ValidFieldSelection_PassesValidation()
    {
        var request = new QueryRequest
        {
            Fields = new FieldSelection { Fields = ["Name", "Length"] },
            Page = new PageRequest { PageSize = 2 },
        };

        PageResult<Widget> result = QueryEngine.Apply(Items, request);

        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public void Validate_FieldNamesAreCaseInsensitive()
    {
        var request = new QueryRequest
        {
            Filters = [new FilterExpression { Field = "name", Operator = FilterOperator.Equals, Value = "Mainline" }],
        };

        PageResult<Widget> result = QueryEngine.Apply(Items, request);

        Assert.Single(result.Items);
    }
}
