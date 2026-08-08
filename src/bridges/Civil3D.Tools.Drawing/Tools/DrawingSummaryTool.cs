using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Drawing.Dtos;

namespace Civil3D.Tools.Drawing.Tools;

/// <summary>
/// Tool <c>drawing_summary</c>: returns a fast, read-only summary of the active drawing — symbol
/// table counts (layers, blocks, text/dimension styles, linetypes, registered applications),
/// model and paper space entity counts, viewport count, external-reference count, named-object
/// dictionary count and the approximate on-disk size. No geometry analysis. Requires an active
/// document; otherwise returns <c>E_NO_ACTIVE_DOCUMENT</c>.
/// </summary>
[McpTool(
    "drawing_summary",
    "Drawing Summary",
    "Returns a fast read-only summary of the active drawing: layer, block, text style, dimension " +
    "style, linetype and registered application counts; model and paper space entity counts; " +
    "viewport, xref and named-object dictionary counts; and the approximate file size. No geometry " +
    "analysis. Fails with E_NO_ACTIVE_DOCUMENT when no drawing is open.",
    Category = ToolCategory.Drawing,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "drawing", "summary", "statistics", "read-only" })]
public sealed class DrawingSummaryTool : Civil3DToolBase<EmptyParameters, DrawingSummaryDto>
{
    private readonly IDrawingStatisticsService _statistics;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="statistics">Drawing statistics service.</param>
    public DrawingSummaryTool(ICivil3DSession session, IDrawingStatisticsService statistics)
        : base(session)
    {
        _statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
    }

    /// <inheritdoc />
    protected override Task<DrawingSummaryDto> ExecuteToolCoreAsync(EmptyParameters input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ActiveDrawing drawing = RequireActiveDrawing(context);
        DrawingStatistics statistics = _statistics.GetStatistics(drawing, cancellationToken);

        return Task.FromResult(new DrawingSummaryDto
        {
            LayerCount = statistics.LayerCount,
            BlockCount = statistics.BlockCount,
            XRefCount = statistics.XRefCount,
            EntityCount = statistics.EntityCount,
            ModelSpaceEntityCount = statistics.ModelSpaceEntityCount,
            PaperSpaceEntityCount = statistics.PaperSpaceEntityCount,
            ViewportCount = statistics.ViewportCount,
            TextStyleCount = statistics.TextStyleCount,
            DimensionStyleCount = statistics.DimensionStyleCount,
            LinetypeCount = statistics.LinetypeCount,
            RegisteredApplicationCount = statistics.RegisteredApplicationCount,
            DictionaryCount = statistics.DictionaryCount,
            ApproximateDrawingSizeBytes = statistics.ApproximateDrawingSizeBytes,
        });
    }
}
