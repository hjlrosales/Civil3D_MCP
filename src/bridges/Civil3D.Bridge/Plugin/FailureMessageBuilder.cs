using System.Text;
using SysException = System.Exception;

namespace Civil3D.Bridge.Plugin;

/// <summary>
/// Renders bridge initialization failures for the alert dialog: a fixed header followed by
/// the exception chain (outer to inner), deduplicating repeated messages. The chain is capped
/// to keep the dialog readable, but the innermost exception (the root cause) is always shown.
/// </summary>
internal static class FailureMessageBuilder
{
    /// <summary>Maximum exception messages surfaced in the alert.</summary>
    private const int MaxDetailLines = 4;

    internal static string Build(SysException exception)
    {
        List<string> messages = CollectMessages(exception);

        var builder = new StringBuilder();
        builder.AppendLine("Civil 3D Bridge failed to initialize. See the bridge log for details.");
        builder.AppendLine();

        if (messages.Count <= MaxDetailLines)
        {
            foreach (string message in messages)
            {
                builder.AppendLine(message);
            }
        }
        else
        {
            // Render the first levels plus the innermost one so the root cause is never cut off.
            int head = MaxDetailLines - 1;
            for (int i = 0; i < head; i++)
            {
                builder.AppendLine(messages[i]);
            }

            builder.AppendLine($"... {messages.Count - head} more level(s); the root cause is:");
            builder.AppendLine(messages[^1]);
        }

        return builder.ToString().TrimEnd();
    }

    private static List<string> CollectMessages(SysException exception)
    {
        var messages = new List<string>();
        string? previousMessage = null;
        for (SysException? current = exception; current is not null; current = current.InnerException)
        {
            string message = current.Message.Trim();
            if (message.Length == 0 || string.Equals(message, previousMessage, StringComparison.Ordinal))
            {
                continue;
            }

            previousMessage = message;
            messages.Add(message);
        }

        return messages;
    }
}
