using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;

namespace Autodesk.Mcp.Sdk.Tests;

/// <summary>Test-only tool with a strongly typed echo input/output.</summary>
[McpTool(
    "test.echo",
    "Echo",
    "Returns the input unchanged.",
    Category = ToolCategory.General,
    Permission = ToolPermission.ReadOnly,
    Risk = ToolRisk.Low,
    Version = "1.2.3",
    Tags = new[] { "test" })]
public sealed class EchoTool : ToolBase<EchoInput, EchoOutput>
{
    /// <inheritdoc />
    protected override Task<EchoOutput> ExecuteCoreAsync(EchoInput input, ToolExecutionContext context, CancellationToken cancellationToken)
        => Task.FromResult(new EchoOutput { Text = input.Text ?? string.Empty, CorrelationId = context.CorrelationId });
}

/// <summary>A tool that cooperates with cancellation.</summary>
[McpTool(
    "test.slow",
    "Slow",
    "Waits until cancelled.",
    Category = ToolCategory.General,
    TimeoutMilliseconds = 500)]
public sealed class SlowTool : ToolBase<EmptyParameters, EchoOutput>
{
    /// <inheritdoc />
    protected override async Task<EchoOutput> ExecuteCoreAsync(EmptyParameters input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return new EchoOutput();
    }
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

    /// <summary>Correlation identifier echoed from the request context.</summary>
    public string? CorrelationId { get; init; }
}
