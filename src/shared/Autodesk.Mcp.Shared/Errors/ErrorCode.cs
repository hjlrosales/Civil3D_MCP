using System.Text.Json.Serialization;
using Autodesk.Mcp.Shared.Serialization;

namespace Autodesk.Mcp.Shared.Errors;

/// <summary>
/// The stable, documented error codes used on the wire. Values are frozen: appending new codes is
/// allowed (protocol minor bump), but renumbering or renaming an existing code is a breaking change.
/// Serialized as the exact member name (for example <c>E_NO_ACTIVE_DOCUMENT</c>); unknown values
/// read from newer peers fall back to <see cref="E_UNKNOWN"/>.
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<ErrorCode>))]
public enum ErrorCode
{
    /// <summary>An unexpected or unmapped error.</summary>
    E_UNKNOWN = 0,

    /// <summary>The operation exceeded its declared timeout.</summary>
    E_TIMEOUT,

    /// <summary>The operation was cancelled.</summary>
    E_CANCELLED,

    /// <summary>The request was malformed or violated the protocol.</summary>
    E_INVALID_REQUEST,

    /// <summary>Input parameters failed structural validation.</summary>
    E_INVALID_PARAMETERS,

    /// <summary>The caller lacks permission for this operation.</summary>
    E_PERMISSION_DENIED,

    /// <summary>The operation requires explicit user confirmation first.</summary>
    E_CONFIRMATION_REQUIRED,

    /// <summary>No active document is available to operate on.</summary>
    E_NO_ACTIVE_DOCUMENT,

    /// <summary>A database transaction failed and was rolled back.</summary>
    E_TRANSACTION_FAILED,

    /// <summary>The requested object does not exist.</summary>
    E_OBJECT_NOT_FOUND,

    /// <summary>Serialization or deserialization of a payload failed.</summary>
    E_SERIALIZATION,

    /// <summary>An internal error occurred on the bridge.</summary>
    E_INTERNAL,

    /// <summary>Inputs failed validation against the tool's JSON Schema.</summary>
    E_VALIDATION_FAILED,

    /// <summary>The bridge is unavailable (offline or unreachable).</summary>
    E_BRIDGE_UNAVAILABLE,
}
