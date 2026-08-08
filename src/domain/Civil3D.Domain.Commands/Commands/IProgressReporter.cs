namespace Civil3D.Domain.Commands;

/// <summary>
/// Reports progress from a long-running command. The tool layer adapts this to the SDK
/// <c>IProgressReporter</c> (which the bridge wires to <c>$/progress</c>); the domain stays
/// protocol-free.
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
