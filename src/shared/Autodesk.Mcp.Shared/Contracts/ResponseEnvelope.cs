using System.Text.Json;
using Autodesk.Mcp.Shared.Errors;

namespace Autodesk.Mcp.Shared.Contracts;

/// <summary>
/// The standard, frozen response envelope returned for every tool execution.
/// A response is either a success (<see cref="Success"/> is true, <see cref="ErrorCode"/> is
/// <c>E_UNKNOWN</c>) or a structured failure (<see cref="Success"/> is false, <see cref="ErrorCode"/>
/// carries the stable wire code). Raw exceptions and stack traces never cross the pipe.
/// </summary>
public sealed record ResponseEnvelope
{
    /// <summary>True when the operation completed successfully.</summary>
    public bool Success { get; init; }

    /// <summary>Human-readable result or failure message; never carries exception details.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Wall-clock execution time reported by the bridge, in milliseconds.</summary>
    public long ExecutionTime { get; init; }

    /// <summary>Stable error code; <see cref="ErrorCode.E_UNKNOWN"/> on success.</summary>
    public ErrorCode ErrorCode { get; init; } = ErrorCode.E_UNKNOWN;

    /// <summary>Correlation identifier echoed from the originating request.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Session identifier echoed from the originating request.</summary>
    public string? SessionId { get; init; }

    /// <summary>Raw result payload (the tool output).</summary>
    public JsonElement? Data { get; init; }

    /// <summary>Creates a success envelope.</summary>
    /// <param name="data">The result payload, if any.</param>
    /// <param name="message">Optional success message.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <param name="sessionId">Optional session identifier.</param>
    /// <param name="executionTime">Optional execution time in milliseconds.</param>
    public static ResponseEnvelope Ok(
        JsonElement? data = null,
        string? message = null,
        string? correlationId = null,
        string? sessionId = null,
        long executionTime = 0)
        => new()
        {
            Success = true,
            Message = message ?? string.Empty,
            Data = data,
            CorrelationId = correlationId,
            SessionId = sessionId,
            ExecutionTime = executionTime,
        };

    /// <summary>Creates a structured failure envelope.</summary>
    /// <param name="errorCode">The stable error code.</param>
    /// <param name="message">A safe, user-visible failure message.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <param name="sessionId">Optional session identifier.</param>
    /// <param name="executionTime">Optional execution time in milliseconds.</param>
    public static ResponseEnvelope Fail(
        ErrorCode errorCode,
        string message,
        string? correlationId = null,
        string? sessionId = null,
        long executionTime = 0)
        => new()
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode,
            CorrelationId = correlationId,
            SessionId = sessionId,
            ExecutionTime = executionTime,
        };
}

/// <summary>
/// Strongly typed variant of <see cref="ResponseEnvelope"/> whose <see cref="ResponseEnvelope{TData}.Data"/>
/// is a typed payload. The wire shape is identical; use this type when the tool output type is known.
/// </summary>
/// <typeparam name="TData">The type of the result payload.</typeparam>
public sealed record ResponseEnvelope<TData>
{
    /// <summary>True when the operation completed successfully.</summary>
    public bool Success { get; init; }

    /// <summary>Human-readable result or failure message; never carries exception details.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Wall-clock execution time reported by the bridge, in milliseconds.</summary>
    public long ExecutionTime { get; init; }

    /// <summary>Stable error code; <see cref="ErrorCode.E_UNKNOWN"/> on success.</summary>
    public ErrorCode ErrorCode { get; init; } = ErrorCode.E_UNKNOWN;

    /// <summary>Correlation identifier echoed from the originating request.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Session identifier echoed from the originating request.</summary>
    public string? SessionId { get; init; }

    /// <summary>Strongly typed result payload.</summary>
    public TData? Data { get; init; }

    /// <summary>Creates a success envelope.</summary>
    /// <param name="data">The result payload, if any.</param>
    /// <param name="message">Optional success message.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <param name="sessionId">Optional session identifier.</param>
    /// <param name="executionTime">Optional execution time in milliseconds.</param>
    public static ResponseEnvelope<TData> Ok(
        TData? data = default,
        string? message = null,
        string? correlationId = null,
        string? sessionId = null,
        long executionTime = 0)
        => new()
        {
            Success = true,
            Message = message ?? string.Empty,
            Data = data,
            CorrelationId = correlationId,
            SessionId = sessionId,
            ExecutionTime = executionTime,
        };

    /// <summary>Creates a structured failure envelope.</summary>
    /// <param name="errorCode">The stable error code.</param>
    /// <param name="message">A safe, user-visible failure message.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <param name="sessionId">Optional session identifier.</param>
    /// <param name="executionTime">Optional execution time in milliseconds.</param>
    public static ResponseEnvelope<TData> Fail(
        ErrorCode errorCode,
        string message,
        string? correlationId = null,
        string? sessionId = null,
        long executionTime = 0)
        => new()
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode,
            CorrelationId = correlationId,
            SessionId = sessionId,
            ExecutionTime = executionTime,
        };
}
