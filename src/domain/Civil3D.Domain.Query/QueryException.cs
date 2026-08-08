namespace Civil3D.Domain.Query;

/// <summary>
/// Raised by <see cref="QueryEngine"/> when a query is malformed: an unknown field name, an
/// operator applied to an unsupported property type, or a missing operator operand. Tools map
/// this to <c>E_INVALID_PARAMETERS</c>.
/// </summary>
public sealed class QueryException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">A human-readable description of the malformed query.</param>
    public QueryException(string message)
        : base(message)
    {
    }
}
