using Civil3D.Bridge.Plugin;
using Xunit;

namespace Civil3D.Bridge.Tests;

/// <summary>
/// Failure-text rendering for the Civil 3D bridge initialization alert: the dialog must
/// surface the root cause (inner exception chain), not just the outer message, and stay
/// readable when the chain is deep or repetitive.
/// </summary>
public class InitializeFailureTests
{
    [Fact]
    public void BuildFailureText_StartsWithStandardHeader()
    {
        string text = FailureMessageBuilder.Build(new InvalidDataException("boom"));

        Assert.StartsWith("Civil 3D Bridge failed to initialize. See the bridge log for details.", text);
    }

    [Fact]
    public void BuildFailureText_IncludesInnerExceptionMessages()
    {
        var jsonError = new InvalidDataException("Could not parse the JSON file.");
        var configError = new InvalidDataException(
            "Failed to load configuration from file 'C:/bundle/Contents/Configuration/bridge.config.json'.",
            jsonError);

        string text = FailureMessageBuilder.Build(configError);

        Assert.Contains("Failed to load configuration from file", text);
        Assert.Contains("Could not parse the JSON file.", text);
    }

    [Fact]
    public void BuildFailureText_DoesNotDuplicateConsecutiveMessages()
    {
        var inner = new InvalidDataException("Same message.");
        var outer = new InvalidDataException("Same message.", inner);

        string text = FailureMessageBuilder.Build(outer);

        Assert.Equal(1, CountOccurrences(text, "Same message."));
    }

    [Fact]
    public void BuildFailureText_CapsDetailLinesButAlwaysShowsRootCause()
    {
        Exception deepest = new("deepest");
        Exception current = deepest;
        for (int i = 0; i < 8; i++)
        {
            current = new Exception($"level-{i}", current);
        }

        string text = FailureMessageBuilder.Build(current);

        // Outermost levels are rendered first, middle levels are elided, and the
        // innermost exception (the root cause) is always included.
        Assert.Contains("level-7", text);
        Assert.DoesNotContain("level-4", text);
        Assert.Contains("deepest", text);
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
