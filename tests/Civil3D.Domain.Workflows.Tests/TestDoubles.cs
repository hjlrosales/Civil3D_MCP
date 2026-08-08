using Civil3D.Domain.Commands;

namespace Civil3D.Domain.Workflows.Tests;

/// <summary>Records every progress report so tests can assert milestone stages.</summary>
internal sealed class RecordingProgressReporter : IProgressReporter
{
    public sealed record ProgressReport(int Percent, string? Stage, string? Message);

    public List<ProgressReport> Reports { get; } = [];

    public void Report(int percent, string? stage = null, string? message = null)
        => Reports.Add(new ProgressReport(percent, stage, message));
}


/// <summary>Captures formatted log messages for a category so tests can assert structured output.</summary>
internal sealed class RecordingLogger<TCategory> : Microsoft.Extensions.Logging.ILogger<TCategory>
{
    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

    public void Log<TState>(
        Microsoft.Extensions.Logging.LogLevel logLevel,
        Microsoft.Extensions.Logging.EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        string message = formatter(state, exception);
        if (message is not null)
        {
            Messages.Add(message);
        }
    }
}
