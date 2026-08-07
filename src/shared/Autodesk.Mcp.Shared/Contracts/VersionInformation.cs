using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Autodesk.Mcp.Shared.Serialization;

namespace Autodesk.Mcp.Shared.Contracts;

/// <summary>
/// A semantic version (SemVer 2.0.0) used for protocol, bridge, SDK and tool versions.
/// Serialized on the wire as the compact string form <c>major.minor.patch[-pre-release][+build]</c>
/// and read back either from that string form or from an object form for tolerance.
/// </summary>
/// <remarks>
/// Precedence follows the SemVer specification: build metadata never affects ordering.
/// Note that the record-generated structural equality <em>does</em> include build metadata,
/// so <c>1.0.0+build1 == 1.0.0+build2</c> is false while <see cref="CompareTo(VersionInformation)"/>
/// reports them as equal in precedence. Use the comparison helpers when ordering matters.
/// </remarks>
[JsonConverter(typeof(VersionInformationConverter))]
public readonly record struct VersionInformation : IComparable<VersionInformation>
{
    /// <summary>The major version component.</summary>
    public int Major { get; }

    /// <summary>The minor version component.</summary>
    public int Minor { get; }

    /// <summary>The patch version component.</summary>
    public int Patch { get; }

    /// <summary>The pre-release component (for example <c>beta.1</c>), or an empty string when absent.</summary>
    public string PreRelease { get; }

    /// <summary>The build metadata component (for example <c>sha.abc123</c>), or an empty string when absent.</summary>
    public string BuildMetadata { get; }

    /// <summary>Creates a semantic version.</summary>
    /// <param name="major">Major component, must be zero or greater.</param>
    /// <param name="minor">Minor component, must be zero or greater.</param>
    /// <param name="patch">Patch component, must be zero or greater.</param>
    /// <param name="preRelease">Optional pre-release component; validated when non-empty.</param>
    /// <param name="buildMetadata">Optional build metadata component; validated when non-empty.</param>
    /// <exception cref="ArgumentOutOfRangeException">When a numeric component is negative.</exception>
    /// <exception cref="ArgumentException">When a non-empty pre-release or build component is malformed.</exception>
    public VersionInformation(int major, int minor, int patch, string? preRelease = null, string? buildMetadata = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(major);
        ArgumentOutOfRangeException.ThrowIfNegative(minor);
        ArgumentOutOfRangeException.ThrowIfNegative(patch);

        string pre = preRelease ?? string.Empty;
        string build = buildMetadata ?? string.Empty;
        if (pre.Length > 0 && !IsValidIdentifierSet(pre))
        {
            throw new ArgumentException("The pre-release component contains invalid identifiers.", nameof(preRelease));
        }

        if (build.Length > 0 && !IsValidIdentifierSet(build))
        {
            throw new ArgumentException("The build metadata component contains invalid identifiers.", nameof(buildMetadata));
        }

        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = pre;
        BuildMetadata = build;
    }

    /// <summary>An all-zero version, used as a "not provided" default on the wire.</summary>
    public static VersionInformation Empty => new(0, 0, 0);

    /// <summary>True when this version carries a pre-release component.</summary>
    public bool IsPreRelease => !string.IsNullOrEmpty(PreRelease);

    /// <summary>
    /// Parses a semantic version string. Accepts two- and three-part core versions
    /// (<c>1.2</c> is treated as <c>1.2.0</c>) for tolerance.
    /// </summary>
    /// <param name="value">The version string to parse.</param>
    /// <returns>The parsed version.</returns>
    /// <exception cref="FormatException">When the value is not a valid semantic version.</exception>
    public static VersionInformation Parse(string value)
    {
        if (!TryParse(value, out VersionInformation result))
        {
            throw new FormatException($"'{value}' is not a valid semantic version.");
        }

        return result;
    }

    /// <summary>
    /// Attempts to parse a semantic version string without throwing.
    /// </summary>
    /// <param name="value">The version string to parse; null or whitespace fails.</param>
    /// <param name="result">The parsed version when successful, otherwise the default value.</param>
    /// <returns>True when parsing succeeded.</returns>
    public static bool TryParse(string? value, out VersionInformation result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string text = value.Trim();

        string build = string.Empty;
        int plusIndex = text.IndexOf('+');
        if (plusIndex >= 0)
        {
            build = text[(plusIndex + 1)..];
            text = text[..plusIndex];
        }

        string pre = string.Empty;
        int dashIndex = text.IndexOf('-');
        string core = text;
        if (dashIndex >= 0)
        {
            pre = text[(dashIndex + 1)..];
            core = text[..dashIndex];
        }

        if (dashIndex >= 0 && pre.Length == 0)
        {
            return false; // A trailing dash with an empty pre-release is invalid.
        }

        string[] parts = core.Split('.');
        if (parts.Length is < 1 or > 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int major) || major < 0)
        {
            return false;
        }

        int minor = 0;
        if (parts.Length >= 2 && (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minor) || minor < 0))
        {
            return false;
        }

        int patch = 0;
        if (parts.Length == 3 && (!int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out patch) || patch < 0))
        {
            return false;
        }

        if ((pre.Length > 0 && !IsValidIdentifierSet(pre)) || (build.Length > 0 && !IsValidIdentifierSet(build)))
        {
            return false;
        }

        result = new VersionInformation(major, minor, patch, pre, build);
        return true;
    }

    /// <summary>
    /// Compares this version to another using SemVer precedence rules.
    /// Build metadata is ignored, matching the specification.
    /// </summary>
    /// <param name="other">The version to compare against.</param>
    /// <returns>A negative value when this version precedes <paramref name="other"/>, zero when they have equal precedence, and a positive value otherwise.</returns>
    public int CompareTo(VersionInformation other)
    {
        int result = Major.CompareTo(other.Major);
        if (result != 0)
        {
            return result;
        }

        result = Minor.CompareTo(other.Minor);
        if (result != 0)
        {
            return result;
        }

        result = Patch.CompareTo(other.Patch);
        if (result != 0)
        {
            return result;
        }

        return ComparePreRelease(PreRelease, other.PreRelease);
    }

    /// <summary>Returns true when <paramref name="left"/> precedes <paramref name="right"/>.</summary>
    public static bool operator <(VersionInformation left, VersionInformation right) => left.CompareTo(right) < 0;

    /// <summary>Returns true when <paramref name="left"/> follows <paramref name="right"/>.</summary>
    public static bool operator >(VersionInformation left, VersionInformation right) => left.CompareTo(right) > 0;

    /// <summary>Returns true when <paramref name="left"/> precedes or equals <paramref name="right"/> in precedence.</summary>
    public static bool operator <=(VersionInformation left, VersionInformation right) => left.CompareTo(right) <= 0;

    /// <summary>Returns true when <paramref name="left"/> follows or equals <paramref name="right"/> in precedence.</summary>
    public static bool operator >=(VersionInformation left, VersionInformation right) => left.CompareTo(right) >= 0;

    /// <summary>
    /// Renders the version in canonical wire form: <c>major.minor.patch[-pre-release][+build]</c>.
    /// </summary>
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(Major).Append('.').Append(Minor).Append('.').Append(Patch);
        if (!string.IsNullOrEmpty(PreRelease))
        {
            builder.Append('-').Append(PreRelease);
        }

        if (!string.IsNullOrEmpty(BuildMetadata))
        {
            builder.Append('+').Append(BuildMetadata);
        }

        return builder.ToString();
    }

    private static int ComparePreRelease(string left, string right)
    {
        bool leftEmpty = string.IsNullOrEmpty(left);
        bool rightEmpty = string.IsNullOrEmpty(right);
        if (leftEmpty && rightEmpty)
        {
            return 0;
        }

        if (leftEmpty)
        {
            return 1; // A release version has higher precedence than any pre-release of the same core.
        }

        if (rightEmpty)
        {
            return -1;
        }

        string[] leftIds = left.Split('.');
        string[] rightIds = right.Split('.');
        int count = Math.Min(leftIds.Length, rightIds.Length);
        for (int i = 0; i < count; i++)
        {
            int cmp = ComparePreReleaseIdentifier(leftIds[i], rightIds[i]);
            if (cmp != 0)
            {
                return cmp;
            }
        }

        return leftIds.Length.CompareTo(rightIds.Length);
    }

    private static int ComparePreReleaseIdentifier(string left, string right)
    {
        bool leftNumeric = long.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out long leftNumber);
        bool rightNumeric = long.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out long rightNumber);

        if (leftNumeric && rightNumeric)
        {
            return leftNumber.CompareTo(rightNumber);
        }

        if (leftNumeric)
        {
            return -1; // Numeric identifiers always sort below alphanumeric identifiers.
        }

        if (rightNumeric)
        {
            return 1;
        }

        return string.CompareOrdinal(left, right);
    }

    private static bool IsValidIdentifierSet(string value)
    {
        foreach (string identifier in value.Split('.'))
        {
            if (identifier.Length == 0)
            {
                return false;
            }

            foreach (char c in identifier)
            {
                if (!char.IsAsciiLetterOrDigit(c) && c != '-')
                {
                    return false;
                }
            }
        }

        return true;
    }
}
