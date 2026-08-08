using System.Text.Json;
using Xunit;

namespace Civil3D.Domain.Query.Tests;

/// <summary>Every supported filter operator, AND semantics and JSON value normalization.</summary>
public class FilteringTests
{
    private static readonly IReadOnlyList<Widget> Items =
    [
        new(1, "Mainline", "Primary road corridor", 1_000, WidgetKind.Road, 5),
        new(2, "Ramp A", "Curved ramp", 300, WidgetKind.Road, 2),
        new(3, "Rail Spur", null, 2_000, WidgetKind.Rail, null),
        new(4, "Utility Duct", "Buried utility duct", 150, WidgetKind.Utility, 1),
    ];

    private static FilterExpression Filter(string field, FilterOperator op, object? value = null, IReadOnlyList<object?>? values = null)
        => new() { Field = field, Operator = op, Value = value, Values = values };

    [Fact]
    public void Equals_IsCaseInsensitiveForStrings()
    {
        var result = QueryEngine.Filter(Items, [Filter("Name", FilterOperator.Equals, "mainline")]);

        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }

    [Fact]
    public void NotEquals_ExcludesMatch()
    {
        var result = QueryEngine.Filter(Items, [Filter("Kind", FilterOperator.NotEquals, "Road")]);

        Assert.Equal([3L, 4L], result.Select(w => w.Id));
    }

    [Fact]
    public void Contains_MatchesSubstringCaseInsensitive()
    {
        var result = QueryEngine.Filter(Items, [Filter("Name", FilterOperator.Contains, "RAMP")]);

        Assert.Equal([2L], result.Select(w => w.Id));
    }

    [Fact]
    public void StartsWith_And_EndsWith()
    {
        Assert.Equal([1L], QueryEngine.Filter(Items, [Filter("Name", FilterOperator.StartsWith, "m")]).Select(w => w.Id));
        Assert.Equal([4L], QueryEngine.Filter(Items, [Filter("Name", FilterOperator.EndsWith, "t")]).Select(w => w.Id));
    }

    [Fact]
    public void GreaterThan_And_LessThan_OnNumbers()
    {
        Assert.Equal([1L, 3L], QueryEngine.Filter(Items, [Filter("Length", FilterOperator.GreaterThan, 500)]).Select(w => w.Id));
        Assert.Equal([2L, 4L], QueryEngine.Filter(Items, [Filter("Length", FilterOperator.LessThan, 500)]).Select(w => w.Id));
    }

    [Fact]
    public void GreaterThanOrEqual_And_LessThanOrEqual()
    {
        Assert.Equal([1L, 2L, 3L, 4L], QueryEngine.Filter(Items, [Filter("Length", FilterOperator.GreaterThanOrEqual, 150)]).Select(w => w.Id));
        Assert.Equal([1L, 2L, 4L], QueryEngine.Filter(Items, [Filter("Length", FilterOperator.LessThanOrEqual, 1_000)]).Select(w => w.Id));
    }

    [Fact]
    public void In_MatchesAnyListedValue()
    {
        var result = QueryEngine.Filter(Items, [Filter("Kind", FilterOperator.In, values: ["Rail", "Utility"])]);

        Assert.Equal([3L, 4L], result.Select(w => w.Id));
    }

    [Fact]
    public void NotIn_ExcludesListedValues()
    {
        var result = QueryEngine.Filter(Items, [Filter("Kind", FilterOperator.NotIn, values: ["Road"])]);

        Assert.Equal([3L, 4L], result.Select(w => w.Id));
    }

    [Fact]
    public void IsNull_And_IsNotNull()
    {
        Assert.Equal([3L], QueryEngine.Filter(Items, [Filter("Rank", FilterOperator.IsNull)]).Select(w => w.Id));
        Assert.Equal([1L, 2L, 4L], QueryEngine.Filter(Items, [Filter("Rank", FilterOperator.IsNotNull)]).Select(w => w.Id));
        Assert.Equal([3L], QueryEngine.Filter(Items, [Filter("Description", FilterOperator.IsNull)]).Select(w => w.Id));
    }

    [Fact]
    public void MultipleFilters_AreAnded()
    {
        var result = QueryEngine.Filter(Items,
        [
            Filter("Kind", FilterOperator.Equals, "Road"),
            Filter("Length", FilterOperator.GreaterThan, 500),
        ]);

        Assert.Equal([1L], result.Select(w => w.Id));
    }

    [Fact]
    public void JsonElementValues_AreNormalized()
    {
        JsonElement jsonNumber = JsonSerializer.Deserialize<JsonElement>("150");
        var result = QueryEngine.Filter(Items, [Filter("Length", FilterOperator.GreaterThan, jsonNumber)]);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void EmptyFilters_ReturnAllItems()
    {
        Assert.Equal(4, QueryEngine.Filter(Items, null).Count);
    }
}
