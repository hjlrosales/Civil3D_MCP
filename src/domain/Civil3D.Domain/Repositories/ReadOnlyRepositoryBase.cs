using Civil3D.Domain.Errors;
using Civil3D.Domain.Query;

namespace Civil3D.Domain.Repositories;

/// <summary>
/// Common base for every read-only domain repository. Provides the standard exception handling
/// shared by all repositories: <see cref="DomainException"/>, <see cref="QueryException"/> and
/// cancellation pass through unchanged, any other failure is logged/mapped to <c>Internal</c> so
/// raw Autodesk exceptions never escape the domain layer. <see cref="QueryException"/> passes
/// through so a malformed query request maps to <c>E_INVALID_PARAMETERS</c> at the tool layer
/// instead of a generic internal error. Repositories never expose Autodesk objects and never edit.
/// </summary>
public abstract class ReadOnlyRepositoryBase
{
    /// <summary>
    /// Executes a repository query with standard exception translation.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="read">The query body (typically a data-source call).</param>
    protected static T ExecuteRead<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (DomainException)
        {
            throw; // Stable domain code already chosen; never remap.
        }
        catch (QueryException)
        {
            throw; // Malformed query: map to E_INVALID_PARAMETERS at the tool layer.
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DomainException(
                DomainErrorCode.Internal,
                "An unexpected error occurred while reading from the drawing database.",
                ex);
        }
    }

    /// <summary>
    /// Returns the value or throws <c>EntityNotFound</c>, the standard repository contract for
    /// single-entity lookups.
    /// </summary>
    /// <typeparam name="T">The entity DTO type.</typeparam>
    /// <param name="value">The lookup result (may be null).</param>
    /// <param name="entityName">Display name of the entity kind, for the message.</param>
    protected static T RequireResult<T>(T? value, string entityName)
        where T : class
        => value ?? throw new DomainException(
            DomainErrorCode.EntityNotFound,
            $"No {entityName} was found.");
}
