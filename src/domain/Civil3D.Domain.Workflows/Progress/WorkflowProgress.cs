using System.Diagnostics;
using Civil3D.Domain.Commands;

namespace Civil3D.Domain.Workflows;

/// <summary>
/// Mutable <see cref="IWorkflowProgress"/> for one workflow execution. Forwards every report to
/// the domain <see cref="Civil3D.Domain.Commands.IProgressReporter"/> seam (the same seam the
/// command framework uses) and tracks percentage, step, message, elapsed and an estimated
/// remaining time. Thread-safe for reads.
/// </summary>
public sealed class WorkflowProgress : IWorkflowProgress
{
    private readonly IProgressReporter _inner;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private int _percent;
    private string _step = string.Empty;
    private string? _message;

    /// <summary>Creates the tracker.</summary>
    /// <param name="inner">The domain progress reporter to forward to.</param>
    public WorkflowProgress(IProgressReporter inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <inheritdoc />
    public int PercentComplete => Volatile.Read(ref _percent);

    /// <inheritdoc />
    public string CurrentStep => Volatile.Read(ref _step);

    /// <inheritdoc />
    public string? CurrentMessage => Volatile.Read(ref _message);

    /// <inheritdoc />
    public TimeSpan Elapsed => _clock.Elapsed;

    /// <inheritdoc />
    public TimeSpan? EstimatedRemaining
    {
        get
        {
            int percent = PercentComplete;
            if (percent <= 0)
            {
                return null;
            }

            double elapsedTicks = _clock.Elapsed.Ticks;
            return TimeSpan.FromTicks((long)(elapsedTicks * (100 - percent) / percent));
        }
    }

    /// <inheritdoc />
    public void Report(int percent, string? step = null, string? message = null)
    {
        int clamped = Math.Clamp(percent, 0, 100);
        Volatile.Write(ref _percent, clamped);
        if (step is not null)
        {
            Volatile.Write(ref _step, step);
        }

        if (message is not null)
        {
            Volatile.Write(ref _message, message);
        }

        _inner.Report(clamped, step ?? _step, message);
    }
}
