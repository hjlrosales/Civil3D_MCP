using Autodesk.Mcp.Sdk.Dispatch;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Enums;
using Autodesk.Mcp.Shared.Serialization;

namespace Autodesk.Mcp.Benchmarks;

/// <summary>A representative read-only tool (echo).</summary>
[McpTool("bench.echo", "Echo", "Returns the input unchanged.", Version = "1.0.0")]
public sealed class EchoTool : ToolBase<EchoInput, EchoOutput>
{
    /// <inheritdoc />
    protected override Task<EchoOutput> ExecuteCoreAsync(EchoInput input, ToolExecutionContext context, CancellationToken cancellationToken)
        => Task.FromResult(new EchoOutput { Text = input.Text ?? string.Empty });
}

/// <summary>A representative listing tool with a filter input and a rich output.</summary>
[McpTool(
    "bench.list_alignments",
    "List Alignments",
    "Lists alignments with optional name filter.",
    Category = ToolCategory.Alignments,
    Version = "1.0.0")]
public sealed class ListAlignmentsTool : ToolBase<ListAlignmentsInput, ListAlignmentsOutput>
{
    /// <inheritdoc />
    protected override Task<ListAlignmentsOutput> ExecuteCoreAsync(ListAlignmentsInput input, ToolExecutionContext context, CancellationToken cancellationToken)
        => Task.FromResult(new ListAlignmentsOutput { Alignments = Array.Empty<AlignmentSummary>() });
}

/// <summary>A representative long-running tool with progress and cancellation.</summary>
[McpTool(
    "bench.calculate_cut_fill",
    "Calculate Cut/Fill",
    "Computes cut and fill volumes between two surfaces.",
    Category = ToolCategory.Surfaces,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Medium,
    Version = "1.0.0",
    TimeoutMilliseconds = 120_000,
    SupportsProgress = true,
    SupportsCancellation = true)]
public sealed class CalculateCutFillTool : ToolBase<CutFillInput, CutFillResult>
{
    /// <inheritdoc />
    protected override Task<CutFillResult> ExecuteCoreAsync(CutFillInput input, ToolExecutionContext context, CancellationToken cancellationToken)
        => Task.FromResult(new CutFillResult { NetVolume = 0 });
}

/// <summary>Input DTO for <see cref="EchoTool"/>.</summary>
public sealed record EchoInput
{
    /// <summary>The text to echo.</summary>
    public string? Text { get; init; }
}

/// <summary>Output DTO for <see cref="EchoTool"/>.</summary>
public sealed record EchoOutput
{
    /// <summary>The echoed text.</summary>
    public string Text { get; init; } = string.Empty;
}

/// <summary>Input DTO for <see cref="ListAlignmentsTool"/>.</summary>
public sealed record ListAlignmentsInput
{
    /// <summary>Optional alignment name filter.</summary>
    public string? Name { get; init; }

    /// <summary>Maximum number of results.</summary>
    public int? Limit { get; init; }
}

/// <summary>Output DTO for <see cref="ListAlignmentsTool"/>.</summary>
public sealed record ListAlignmentsOutput
{
    /// <summary>The alignments found.</summary>
    public AlignmentSummary[] Alignments { get; init; } = Array.Empty<AlignmentSummary>();
}

/// <summary>One alignment summary row.</summary>
public sealed record AlignmentSummary
{
    /// <summary>The alignment id.</summary>
    public long ObjectId { get; init; }

    /// <summary>The alignment name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Length in drawing units.</summary>
    public double Length { get; init; }

    /// <summary>Station of the start point.</summary>
    public double StartStation { get; init; }

    /// <summary>Station of the end point.</summary>
    public double EndStation { get; init; }
}

/// <summary>Input DTO for <see cref="CalculateCutFillTool"/>.</summary>
public sealed record CutFillInput
{
    /// <summary>Existing ground surface name.</summary>
    public string ExistingSurface { get; init; } = string.Empty;

    /// <summary>Proposed surface name.</summary>
    public string ProposedSurface { get; init; } = string.Empty;

    /// <summary>Grid spacing in drawing units.</summary>
    public double? GridSpacing { get; init; }
}

/// <summary>Output DTO for <see cref="CalculateCutFillTool"/>.</summary>
public sealed record CutFillResult
{
    /// <summary>Net volume (positive = cut, negative = fill).</summary>
    public double NetVolume { get; init; }

    /// <summary>Total cut volume.</summary>
    public double CutVolume { get; init; }

    /// <summary>Total fill volume.</summary>
    public double FillVolume { get; init; }
}

/// <summary>Executes tool invocations inline (no application-context marshaling needed here).</summary>
public sealed class InlineExecutor : IToolExecutor
{
    /// <inheritdoc />
    public async Task<ResponseEnvelope> ExecuteAsync(ToolInvocation invocation, CancellationToken cancellationToken)
    {
        var tool = new EchoTool();
        var context = new ToolExecutionContext
        {
            ToolName = invocation.ToolName,
            CorrelationId = invocation.CorrelationId ?? string.Empty,
            CancellationToken = cancellationToken,
        };
        object? result = await tool.ExecuteAsync(context, invocation.Parameters);
        return ResponseEnvelope.Ok(
            data: System.Text.Json.JsonSerializer.SerializeToElement(result, SharedJson.Options),
            correlationId: invocation.CorrelationId);
    }
}
