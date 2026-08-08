namespace Autodesk.Mcp.Sdk.Tools;

/// <summary>
/// Reports progress from a long-running tool. The bridge wires this to <c>$/progress</c>
/// notifications; until then the null implementation discards reports.
/// </summary>
public interface IProgressReporter
{
    /// <summary>Reports completion percentage and an optional stage label.</summary>
    void Report(int percent, string? stage = null, string? message = null);
}

/// <summary>A no-op <see cref="IProgressReporter"/> used when progress is not wired up.</summary>
public sealed class NullProgressReporter : IProgressReporter
{
    private NullProgressReporter() { }

    /// <summary>The shared instance.</summary>
    public static NullProgressReporter Instance { get; } = new();

    /// <inheritdoc />
    public void Report(int percent, string? stage = null, string? message = null) { }
}
