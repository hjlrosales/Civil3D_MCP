using Autodesk.Mcp.Shared.Errors;

namespace Autodesk.Mcp.Shared.Extensions;

/// <summary>
/// Conversion helpers between <see cref="ErrorCode"/> values and their wire representation.
/// Wire values are the exact enum member names (for example <c>E_TIMEOUT</c>); parsing is
/// case-insensitive and tolerates unknown values by falling back to <see cref="ErrorCode.E_UNKNOWN"/>.
/// </summary>
public static class ErrorCodeExtensions
{
    /// <summary>Returns the stable wire representation of an error code (the member name).</summary>
    /// <param name="code">The error code.</param>
    public static string ToWireString(this ErrorCode code) => code.ToString();

    /// <summary>
    /// Parses a wire representation into an <see cref="ErrorCode"/>. Case-insensitive;
    /// unrecognized values map to <see cref="ErrorCode.E_UNKNOWN"/> rather than throwing.
    /// </summary>
    /// <param name="wireValue">The wire value.</param>
    public static ErrorCode FromWireString(string wireValue)
        => TryFromWireString(wireValue, out ErrorCode code) ? code : ErrorCode.E_UNKNOWN;

    /// <summary>
    /// Attempts to parse a wire representation into an <see cref="ErrorCode"/>.
    /// </summary>
    /// <param name="wireValue">The wire value; null or whitespace fails.</param>
    /// <param name="code">The parsed code when successful, otherwise <see cref="ErrorCode.E_UNKNOWN"/>.</param>
    /// <returns>True when the value was recognized.</returns>
    public static bool TryFromWireString(string? wireValue, out ErrorCode code)
    {
        if (!string.IsNullOrWhiteSpace(wireValue) && Enum.TryParse(wireValue, ignoreCase: true, out ErrorCode parsed))
        {
            code = parsed;
            return true;
        }

        code = ErrorCode.E_UNKNOWN;
        return false;
    }
}
