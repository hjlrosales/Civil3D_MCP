using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Autodesk.Mcp.Shared.Errors;
using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Alignments.Services;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Query.Dtos;

namespace Civil3D.Tools.Query.Tools;

/// <summary>
/// Tool <c>get_alignment</c>: returns a single alignment by its stable numeric id. Read-only.
/// Fails with E_NO_ACTIVE_DOCUMENT when no drawing is open and E_OBJECT_NOT_FOUND when the id
/// does not exist.
/// </summary>
[McpTool(
    "get_alignment",
    "Get Alignment",
    "Returns a single alignment by id. Read-only; fails with E_NO_ACTIVE_DOCUMENT when no drawing " +
    "is open and E_OBJECT_NOT_FOUND when the id does not exist.",
    Category = ToolCategory.Alignments,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.0.0",
    SupportsCancellation = true,
    Tags = new[] { "alignments", "lookup", "read-only" })]
public sealed class GetAlignmentTool : QueryToolBase<IdRequest, AlignmentInfo>
{
    private readonly IAlignmentService _alignments;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="alignments">The alignment domain service.</param>
    public GetAlignmentTool(ICivil3DSession session, IAlignmentService alignments)
        : base(session)
    {
        _alignments = alignments ?? throw new ArgumentNullException(nameof(alignments));
    }

    /// <inheritdoc />
    protected override Task<AlignmentInfo> ExecuteToolCoreAsync(
        IdRequest input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireActiveDrawing(context);

        AlignmentInfo? result = RunQuery(context, () => _alignments.GetById(input.Id));
        return Task.FromResult(result ?? throw new BridgeException(
            ErrorCode.E_OBJECT_NOT_FOUND,
            $"No alignment with id {input.Id} was found.",
            context.CorrelationId,
            context.SessionId));
    }
}
