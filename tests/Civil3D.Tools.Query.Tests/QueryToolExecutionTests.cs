using System.Text.Json;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Errors;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Cogo.Dtos;
using Civil3D.Domain.Query;
using Civil3D.Domain.Styles.Dtos;
using Civil3D.Tools.Query.Dtos;
using Xunit;

namespace Civil3D.Tools.Query.Tests;

/// <summary>
/// End-to-end query tool execution through the real dispatcher: request routing, dispatcher,
/// tool, domain service (real QueryEngine) and the protocol response envelope. Also covers the
/// standard error mapping (E_NO_ACTIVE_DOCUMENT, E_OBJECT_NOT_FOUND, E_INVALID_PARAMETERS).
/// </summary>
public class QueryToolExecutionTests
{
    [Fact]
    public async Task ListAlignments_AppliesFilterAndPaging_ReturnsPageResult()
    {
        var dispatcher = QueryTestHarness.CreateDispatcher(QueryTestHarness.CreateCatalog());

        object payload = new
        {
            filters = new[]
            {
                new { field = "Name", @operator = (int)FilterOperator.Contains, value = "Main" },
            },
            page = new { page = 1, pageSize = 10 },
        };
        ResponseEnvelope response = await dispatcher.ExecuteAsync(
            QueryTestHarness.Invoke("list_alignments", payload), CancellationToken.None);

        Assert.True(response.Success, response.Message);
        PageResult<AlignmentInfo>? result =
            response.Data?.Deserialize<PageResult<AlignmentInfo>>(SharedJson.Options);
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal("Mainline", result.Items[0].Name);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.Page);
    }

    [Fact]
    public async Task ListStyles_SortsByNameDescending()
    {
        var dispatcher = QueryTestHarness.CreateDispatcher(QueryTestHarness.CreateCatalog());

        object payload = new
        {
            sorts = new[]
            {
                new { field = "Name", direction = (int)SortDirection.Descending },
            },
        };
        ResponseEnvelope response = await dispatcher.ExecuteAsync(
            QueryTestHarness.Invoke("list_styles", payload), CancellationToken.None);

        Assert.True(response.Success, response.Message);
        PageResult<StyleInfo>? result =
            response.Data?.Deserialize<PageResult<StyleInfo>>(SharedJson.Options);
        Assert.NotNull(result);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(["Surface Style", "Road Style", "Code Set"], result.Items.Select(s => s.Name));
    }

    [Fact]
    public async Task ListAlignments_InvalidFilterField_ReturnsInvalidParameters()
    {
        var dispatcher = QueryTestHarness.CreateDispatcher(QueryTestHarness.CreateCatalog());

        object payload = new
        {
            filters = new[]
            {
                new { field = "NoSuchField", @operator = (int)FilterOperator.Equals, value = "x" },
            },
        };
        ResponseEnvelope response = await dispatcher.ExecuteAsync(
            QueryTestHarness.Invoke("list_alignments", payload), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.E_INVALID_PARAMETERS, response.ErrorCode);
    }

    [Fact]
    public async Task GetAlignment_ExistingId_ReturnsObject()
    {
        var dispatcher = QueryTestHarness.CreateDispatcher(QueryTestHarness.CreateCatalog());

        ResponseEnvelope response = await dispatcher.ExecuteAsync(
            QueryTestHarness.Invoke("get_alignment", new { id = 1L }), CancellationToken.None);

        Assert.True(response.Success, response.Message);
        AlignmentInfo? result = response.Data?.Deserialize<AlignmentInfo>(SharedJson.Options);
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("Mainline", result.Name);
    }

    [Fact]
    public async Task GetAlignment_MissingId_ReturnsObjectNotFound()
    {
        var dispatcher = QueryTestHarness.CreateDispatcher(QueryTestHarness.CreateCatalog());

        ResponseEnvelope response = await dispatcher.ExecuteAsync(
            QueryTestHarness.Invoke("get_alignment", new { id = 999L }), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.E_OBJECT_NOT_FOUND, response.ErrorCode);
    }

    [Fact]
    public async Task ListAlignments_WithoutActiveDocument_ReturnsNoActiveDocument()
    {
        var noDocument = new FakeServices.FakeSession(drawing: null);
        var dispatcher = QueryTestHarness.CreateDispatcher(QueryTestHarness.CreateCatalog(noDocument));

        ResponseEnvelope response = await dispatcher.ExecuteAsync(
            QueryTestHarness.Invoke("list_alignments"), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.E_NO_ACTIVE_DOCUMENT, response.ErrorCode);
    }

    [Fact]
    public async Task SearchObjects_MatchesName_ResolvesStyle_AndPages()
    {
        var dispatcher = QueryTestHarness.CreateDispatcher(QueryTestHarness.CreateCatalog());

        object payload = new
        {
            query = "Mainline",
            kinds = new[] { "alignment" },
            page = new { page = 1, pageSize = 50 },
        };
        ResponseEnvelope response = await dispatcher.ExecuteAsync(
            QueryTestHarness.Invoke("search_objects", payload), CancellationToken.None);

        Assert.True(response.Success, response.Message);
        SearchResult<ObjectReference>? result =
            response.Data?.Deserialize<SearchResult<ObjectReference>>(SharedJson.Options);
        Assert.NotNull(result);
        ObjectReference hit = Assert.Single(result.Items);
        Assert.Equal("alignment", hit.Kind);
        Assert.Equal("Mainline", hit.Name);
        Assert.Equal("Road Style", hit.StyleName);
    }

    [Fact]
    public async Task SearchObjects_KindFilter_IsCaseInsensitive()
    {
        var dispatcher = QueryTestHarness.CreateDispatcher(QueryTestHarness.CreateCatalog());

        object payload = new
        {
            query = "Mainline",
            kinds = new[] { "ALIGNMENT" },
        };
        ResponseEnvelope response = await dispatcher.ExecuteAsync(
            QueryTestHarness.Invoke("search_objects", payload), CancellationToken.None);

        Assert.True(response.Success, response.Message);
        SearchResult<ObjectReference>? result =
            response.Data?.Deserialize<SearchResult<ObjectReference>>(SharedJson.Options);
        Assert.NotNull(result);
        Assert.Equal("alignment", Assert.Single(result.Items).Kind);
    }

    [Fact]
    public async Task SearchObjects_MatchesCogoPointByDescription()
    {
        var dispatcher = QueryTestHarness.CreateDispatcher(QueryTestHarness.CreateCatalog());

        object payload = new
        {
            query = "Benchmark",
            kinds = new[] { "cogo_point" },
        };
        ResponseEnvelope response = await dispatcher.ExecuteAsync(
            QueryTestHarness.Invoke("search_objects", payload), CancellationToken.None);

        Assert.True(response.Success, response.Message);
        SearchResult<ObjectReference>? result =
            response.Data?.Deserialize<SearchResult<ObjectReference>>(SharedJson.Options);
        Assert.NotNull(result);
        ObjectReference hit = Assert.Single(result.Items);
        Assert.Equal("cogo_point", hit.Kind);
        Assert.Equal("Point 101", hit.Name);
        Assert.Equal("Benchmark", hit.Description);
    }

    [Fact]
    public async Task SearchObjects_EmptyQuery_ReturnsInvalidParameters()
    {
        var dispatcher = QueryTestHarness.CreateDispatcher(QueryTestHarness.CreateCatalog());

        ResponseEnvelope response = await dispatcher.ExecuteAsync(
            QueryTestHarness.Invoke("search_objects", new { query = "  " }), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.E_INVALID_PARAMETERS, response.ErrorCode);
    }

    [Fact]
    public async Task SearchObjects_UnknownKind_ReturnsInvalidParameters()
    {
        var dispatcher = QueryTestHarness.CreateDispatcher(QueryTestHarness.CreateCatalog());

        object payload = new
        {
            query = "Mainline",
            kinds = new[] { "gizmo" },
        };
        ResponseEnvelope response = await dispatcher.ExecuteAsync(
            QueryTestHarness.Invoke("search_objects", payload), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.E_INVALID_PARAMETERS, response.ErrorCode);
    }

    [Fact]
    public async Task GetCogoPoint_ExistingId_ReturnsPoint()
    {
        var dispatcher = QueryTestHarness.CreateDispatcher(QueryTestHarness.CreateCatalog());

        ResponseEnvelope response = await dispatcher.ExecuteAsync(
            QueryTestHarness.Invoke("get_cogo_point", new { id = 1L }), CancellationToken.None);

        Assert.True(response.Success, response.Message);
        CogoPointInfo? result = response.Data?.Deserialize<CogoPointInfo>(SharedJson.Options);
        Assert.NotNull(result);
        Assert.Equal(101u, result.PointNumber);
    }

    [Fact]
    public async Task UnknownTool_ReturnsObjectNotFound()
    {
        var dispatcher = QueryTestHarness.CreateDispatcher(QueryTestHarness.CreateCatalog());

        ResponseEnvelope response = await dispatcher.ExecuteAsync(
            QueryTestHarness.Invoke("no_such_tool"), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.E_OBJECT_NOT_FOUND, response.ErrorCode);
    }
}
