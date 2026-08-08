using System.Text;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Autodesk.Mcp.Shared.Errors;
using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Alignments.Services;
using Civil3D.Domain.Cogo.Dtos;
using Civil3D.Domain.Cogo.Services;
using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Domain.Corridors.Services;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Pipes.Services;
using Civil3D.Domain.Profiles.Dtos;
using Civil3D.Domain.Profiles.Services;
using Civil3D.Domain.Query;
using Civil3D.Domain.Styles.Dtos;
using Civil3D.Domain.Styles.Services;
using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Domain.Surfaces.Services;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Query.Dtos;

namespace Civil3D.Tools.Query.Tools;

/// <summary>
/// Tool <c>search_objects</c>: free-text search across the selected entity kinds in the active
/// drawing. Matches the object name and description (case-insensitive contains); when the kind
/// carries a style reference, the resolved style name is also matched. Returns typed
/// <see cref="ObjectReference"/> hits. No geometry searching. The <c>Layer</c> field is not yet
/// available from the domain DTOs and is always null. Read-only; fails with E_NO_ACTIVE_DOCUMENT
/// when no drawing is open and E_INVALID_PARAMETERS for an empty query or unknown kind.
/// </summary>
[McpTool(
    "search_objects",
    "Search Objects",
    "Searches objects in the active drawing by name and description (and style name where the " +
    "kind is styled). Accepts a SearchRequest with an optional kind filter and pagination. " +
    "Read-only; fails with E_NO_ACTIVE_DOCUMENT when no drawing is open.",
    Category = ToolCategory.Objects,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "search", "objects", "query", "read-only" })]
public sealed class SearchObjectsTool : QueryToolBase<SearchRequest, SearchResult<ObjectReference>>
{
    private static readonly string[] AllKinds =
    [
        "alignment", "surface", "profile", "corridor", "pipe_network", "cogo_point", "style",
    ];

    private readonly IAlignmentService _alignments;
    private readonly ISurfaceService _surfaces;
    private readonly IProfileService _profiles;
    private readonly ICorridorService _corridors;
    private readonly IPipeService _pipes;
    private readonly ICogoService _cogo;
    private readonly IStyleService _styles;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="alignments">The alignment domain service.</param>
    /// <param name="surfaces">The surface domain service.</param>
    /// <param name="profiles">The profile domain service.</param>
    /// <param name="corridors">The corridor domain service.</param>
    /// <param name="pipes">The pipe network domain service.</param>
    /// <param name="cogo">The COGO domain service.</param>
    /// <param name="styles">The style domain service.</param>
    public SearchObjectsTool(
        ICivil3DSession session,
        IAlignmentService alignments,
        ISurfaceService surfaces,
        IProfileService profiles,
        ICorridorService corridors,
        IPipeService pipes,
        ICogoService cogo,
        IStyleService styles)
        : base(session)
    {
        _alignments = alignments ?? throw new ArgumentNullException(nameof(alignments));
        _surfaces = surfaces ?? throw new ArgumentNullException(nameof(surfaces));
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _corridors = corridors ?? throw new ArgumentNullException(nameof(corridors));
        _pipes = pipes ?? throw new ArgumentNullException(nameof(pipes));
        _cogo = cogo ?? throw new ArgumentNullException(nameof(cogo));
        _styles = styles ?? throw new ArgumentNullException(nameof(styles));
    }

    /// <inheritdoc />
    protected override Task<SearchResult<ObjectReference>> ExecuteToolCoreAsync(
        SearchRequest input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireActiveDrawing(context);

        if (string.IsNullOrWhiteSpace(input.Query))
        {
            throw new BridgeException(
                ErrorCode.E_INVALID_PARAMETERS,
                "The search query must not be empty.",
                context.CorrelationId,
                context.SessionId);
        }

        string[] kinds = input.Kinds is { Count: > 0 } selected ? selected.ToArray() : AllKinds;
        foreach (string kind in kinds)
        {
            if (!AllKinds.Contains(kind, StringComparer.OrdinalIgnoreCase))
            {
                throw new BridgeException(
                    ErrorCode.E_INVALID_PARAMETERS,
                    $"Unknown kind '{kind}'. Valid kinds: {string.Join(", ", AllKinds)}.",
                    context.CorrelationId,
                    context.SessionId);
            }
        }

        // Normalize once after validation so the switch below is case-insensitive too
        // ("ALIGNMENT" must behave exactly like "alignment").
        string[] normalizedKinds = kinds.Select(static k => k.ToLowerInvariant()).ToArray();

        string query = input.Query;
        var styleNames = new Dictionary<long, string>();
        foreach (StyleInfo style in RunQuery(context, () => _styles.GetAll().Items))
        {
            styleNames[style.Id] = style.Name;
        }

        var matches = new List<ObjectReference>();
        foreach (string kind in normalizedKinds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CollectKind(kind, query, styleNames, matches, context);
        }

        PageResult<ObjectReference> page = QueryEngine.Apply(
            matches, new QueryRequest { Page = input.Page ?? new PageRequest() });
        return Task.FromResult(new SearchResult<ObjectReference>(
            page.Items, page.Page, page.PageSize, page.TotalCount)
        {
            Statistics = page.Statistics,
        });
    }

    private void CollectKind(
        string kind,
        string query,
        IReadOnlyDictionary<long, string> styleNames,
        List<ObjectReference> matches,
        ToolExecutionContext context)
    {
        switch (kind)
        {
            case "alignment":
                Collect(RunQuery(context, () => _alignments.GetAll().Items), query,
                    ["Name", "Description"], matches,
                    a => new ObjectReference
                    {
                        Kind = "alignment",
                        Id = a.Id,
                        Name = a.Name,
                        Description = a.Description,
                        StyleName = ResolveStyle(styleNames, a.StyleId),
                    },
                    a => ResolveStyle(styleNames, a.StyleId));
                break;

            case "surface":
                Collect(RunQuery(context, () => _surfaces.GetAll().Items), query,
                    ["Name", "Description"], matches,
                    s => new ObjectReference
                    {
                        Kind = "surface",
                        Id = s.Id,
                        Name = s.Name,
                        Description = s.Description,
                    });
                break;

            case "profile":
                Collect(RunQuery(context, () => _profiles.GetAll().Items), query,
                    ["Name", "Description"], matches,
                    p => new ObjectReference
                    {
                        Kind = "profile",
                        Id = p.Id,
                        Name = p.Name,
                        Description = p.Description,
                    });
                break;

            case "corridor":
                Collect(RunQuery(context, () => _corridors.GetAll().Items), query,
                    ["Name", "Description"], matches,
                    c => new ObjectReference
                    {
                        Kind = "corridor",
                        Id = c.Id,
                        Name = c.Name,
                        Description = c.Description,
                        StyleName = ResolveStyle(styleNames, c.StyleId),
                    },
                    c => ResolveStyle(styleNames, c.StyleId));
                break;

            case "pipe_network":
                Collect(RunQuery(context, () => _pipes.GetAll().Items), query,
                    ["Name", "Description"], matches,
                    n => new ObjectReference
                    {
                        Kind = "pipe_network",
                        Id = n.Id,
                        Name = n.Name,
                        Description = n.Description,
                    });
                break;

            case "cogo_point":
                Collect(RunQuery(context, () => _cogo.GetAll().Items), query,
                    ["PointNumber", "FullDescription"], matches,
                    p => new ObjectReference
                    {
                        Kind = "cogo_point",
                        Id = p.Id,
                        Name = $"Point {p.PointNumber}",
                        Description = p.FullDescription,
                    });
                break;

            case "style":
                Collect(RunQuery(context, () => _styles.GetAll().Items), query,
                    ["Name", "Description"], matches,
                    st => new ObjectReference
                    {
                        Kind = "style",
                        Id = st.Id,
                        Name = st.Name,
                        Description = st.Description,
                    });
                break;
        }
    }

    private static void Collect<TDto>(
        IReadOnlyList<TDto> items,
        string query,
        string[] searchFields,
        List<ObjectReference> matches,
        Func<TDto, ObjectReference> map,
        Func<TDto, string?>? styleName = null)
    {
        foreach (TDto item in items)
        {
            bool hit = QueryEngine.MatchesSearch(item, query, searchFields)
                || (styleName?.Invoke(item) is { } name
                    && name.Contains(query, StringComparison.OrdinalIgnoreCase));
            if (hit)
            {
                matches.Add(map(item));
            }
        }
    }

    private static string? ResolveStyle(IReadOnlyDictionary<long, string> styleNames, long? styleId)
        => styleId is { } id && styleNames.TryGetValue(id, out string? name) ? name : null;
}
