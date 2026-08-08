using Civil3D.Domain.Errors;

namespace Civil3D.Domain.Services;

/// <summary>
/// Common base for every domain service. Services translate repository
/// <see cref="DomainException"/>s into business results; this base provides the single
/// translation rule shared by all services: <c>EntityNotFound</c> becomes a null business result.
/// </summary>
public abstract class DomainServiceBase
{
    /// <summary>
    /// Executes a repository read, translating <c>EntityNotFound</c> into a null result. Other
    /// domain errors (for example <c>NoActiveDocument</c>) propagate so the caller can translate
    /// them further (tools map them to protocol responses).
    /// </summary>
    /// <typeparam name="T">The entity DTO type.</typeparam>
    /// <param name="read">The repository call.</param>
    protected static T? NotFoundAsNull<T>(Func<T> read)
        where T : class
    {
        try
        {
            return read();
        }
        catch (DomainException ex) when (ex.Code == DomainErrorCode.EntityNotFound)
        {
            return null;
        }
    }
}
