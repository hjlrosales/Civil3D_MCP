using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Tools;
using Civil3D.Tools.Query.Tools;
using Xunit;

namespace Civil3D.Tools.Query.Tests;

/// <summary>
/// Tool discovery, catalog registration and manifest generation for the 15 query tools
/// (7 list, 7 get, 1 search).
/// </summary>
public class QueryToolDiscoveryTests
{
    private static readonly Type[] AllToolTypes =
    [
        typeof(ListAlignmentsTool), typeof(ListProfilesTool), typeof(ListSurfacesTool),
        typeof(ListCorridorsTool), typeof(ListPipeNetworksTool), typeof(ListCogoPointsTool),
        typeof(ListStylesTool), typeof(GetAlignmentTool), typeof(GetProfileTool),
        typeof(GetSurfaceTool), typeof(GetCorridorTool), typeof(GetPipeNetworkTool),
        typeof(GetCogoPointTool), typeof(GetStyleTool), typeof(SearchObjectsTool),
    ];

    private static readonly string[] AllToolNames =
    [
        "list_alignments", "list_profiles", "list_surfaces", "list_corridors",
        "list_pipe_networks", "list_cogo_points", "list_styles", "get_alignment",
        "get_profile", "get_surface", "get_corridor", "get_pipe_network",
        "get_cogo_point", "get_style", "search_objects",
    ];

    [Fact]
    public void Scanner_FindsAllFifteenQueryTools()
    {
        IReadOnlyList<Type> types = ToolScanner.FindToolTypes(new[] { typeof(ListAlignmentsTool).Assembly });

        Assert.Equal(15, types.Count);
        foreach (Type type in AllToolTypes)
        {
            Assert.Contains(types, t => t == type);
        }
    }

    [Fact]
    public void Catalog_ResolvesAllQueryTools()
    {
        ToolCatalog catalog = QueryTestHarness.CreateCatalog();

        foreach (string name in AllToolNames)
        {
            Assert.True(catalog.TryGetTool(name, out ITool? tool), name);
            Assert.Equal(name, tool!.Name);
        }
    }

    [Fact]
    public void Catalog_ExposesQueryManifests()
    {
        ToolCatalog catalog = QueryTestHarness.CreateCatalog();

        Assert.Equal(15, catalog.ToolNames.Count);
        foreach (string name in AllToolNames)
        {
            Assert.NotNull(catalog.GetManifest(name));
        }
    }
}
