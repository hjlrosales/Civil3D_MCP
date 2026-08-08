using Xunit;

namespace Civil3D.Domain.Query.Tests;

/// <summary>Single and multi-key sorting with stable tie-breaking.</summary>
public class SortingTests
{
    private static readonly IReadOnlyList<Widget> Items =
    [
        new(1, "Beta", "b", 200, WidgetKind.Road, 2),
        new(2, "Alpha", "a", 100, WidgetKind.Road, 1),
        new(3, "Beta", "b2", 300, WidgetKind.Rail, null),
        new(4, "Gamma", "g", 100, WidgetKind.Utility, 3),
    ];

    private static SortExpression Sort(string field, SortDirection direction = SortDirection.Ascending)
        => new() { Field = field, Direction = direction };

    [Fact]
    public void Ascending_OrdersByField()
    {
        var result = QueryEngine.Sort(Items, [Sort("Name")]);

        Assert.Equal([2L, 1L, 3L, 4L], result.Select(w => w.Id));
    }

    [Fact]
    public void Descending_OrdersByFieldReversed()
    {
        var result = QueryEngine.Sort(Items, [Sort("Name", SortDirection.Descending)]);

        Assert.Equal([4L, 1L, 3L, 2L], result.Select(w => w.Id));
    }

    [Fact]
    public void NumericSort_UsesNumericOrder()
    {
        var result = QueryEngine.Sort(Items, [Sort("Length")]);

        Assert.Equal([2L, 4L, 1L, 3L], result.Select(w => w.Id));
    }

    [Fact]
    public void MultiKeySort_FirstKeyIsPrimary()
    {
        var result = QueryEngine.Sort(Items, [Sort("Kind"), Sort("Length")]);

        // Road (Road=0: 2 by length then 1), Rail (3), Utility (4).
        Assert.Equal([2L, 1L, 3L, 4L], result.Select(w => w.Id));
    }

    [Fact]
    public void Sort_IsStable_ForEqualKeys()
    {
        var result = QueryEngine.Sort(Items, [Sort("Name")]);

        // Items 1 and 3 share the name "Beta": original order is preserved.
        Assert.Equal([1L, 3L], result.Where(w => w.Name == "Beta").Select(w => w.Id));
    }

    [Fact]
    public void NullValues_SortFirstAscending()
    {
        var result = QueryEngine.Sort(Items, [Sort("Rank")]);

        Assert.Equal([3L], result.Take(1).Select(w => w.Id));
    }

    [Fact]
    public void EmptySorts_PreserveOriginalOrder()
    {
        var result = QueryEngine.Sort(Items, null);

        Assert.Equal([1L, 2L, 3L, 4L], result.Select(w => w.Id));
    }
}
