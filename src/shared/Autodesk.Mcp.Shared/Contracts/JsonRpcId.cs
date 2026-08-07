using System.Text.Json.Serialization;
using Autodesk.Mcp.Shared.Serialization;

namespace Autodesk.Mcp.Shared.Contracts;

/// <summary>
/// A JSON-RPC 2.0 request identifier. The protocol accepts either a numeric or a string id on the wire;
/// this type keeps both shapes round-trip safe so the Bridge and the MCP Server never disagree on correlation.
/// </summary>
[JsonConverter(typeof(JsonRpcIdConverter))]
public readonly record struct JsonRpcId
{
    private readonly long? _number;
    private readonly string? _text;

    /// <summary>Creates a numeric identifier.</summary>
    public JsonRpcId(long number)
    {
        _number = number;
        _text = null;
    }

    /// <summary>Creates a string identifier.</summary>
    public JsonRpcId(string text)
    {
        _number = null;
        _text = text ?? throw new ArgumentNullException(nameof(text));
    }

    /// <summary>An identifier that is neither numeric nor textual (used for notifications, which carry no id).</summary>
    public static JsonRpcId Null => default;

    /// <summary>True when this identifier represents the absence of an id (JSON-RPC notification).</summary>
    public bool IsNull => _number is null && _text is null;

    /// <summary>True when the identifier was created from a number.</summary>
    public bool IsNumber => _number.HasValue;

    /// <summary>True when the identifier was created from a string.</summary>
    public bool IsString => _text is not null;

    /// <summary>Returns the numeric value when <see cref="IsNumber"/> is true, otherwise null.</summary>
    public long? AsNumber() => _number;

    /// <summary>Returns the string value when <see cref="IsString"/> is true, otherwise null.</summary>
    public string? AsString() => _text;

    /// <inheritdoc />
    public override string ToString() => _text ?? _number?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<null>";

    /// <summary>Implicit conversion from <see cref="long"/>.</summary>
    public static implicit operator JsonRpcId(long value) => new(value);

    /// <summary>Implicit conversion from <see cref="string"/>.</summary>
    public static implicit operator JsonRpcId(string value) => new(value);
}
