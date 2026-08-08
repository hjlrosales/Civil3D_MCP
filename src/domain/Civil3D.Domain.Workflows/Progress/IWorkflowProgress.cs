namespace Civil3D.Domain.Workflows;

/// <summary>
/// Reports and tracks progress of a long-running workflow: completion percentage, current step
/// and message, elapsed time and (when enough progress is known) an estimated remaining time.
/// The tool layer adapts this to the SDK <c>IProgressReporter</c> (which the bridge wires to
/// <c>$/progress</c>); the domain stays protocol-free.
/// </summary>
public interface IWorkflowProgress
{
    /// <summary>Completion percentage in the range 0–100.</summary>
    int PercentComplete { get; }

    /// <summary>The currently executing step name.</summary>
    string CurrentStep { get; }

    /// <summary>The most recent progress message, when supplied.</summary>
    string? CurrentMessage { get; }

    /// <summary>Elapsed time since the progress tracker was created.</summary>
    TimeSpan Elapsed { get; }

    /// <summary>Estimated remaining time, when derivable from current progress.</summary>
    TimeSpan? EstimatedRemaining { get; }

    /// <summary>Reports progress.</summary>
    /// <param name="percent">Completion percentage, clamped to 0–100.</param>
    /// <param name="step">Optional current step name.</param>
    /// <param name="message">Optional human-readable progress message.</param>
    void Report(int percent, string? step = null, string? message = null);
}
