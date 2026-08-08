namespace Civil3D.Domain.Errors;

/// <summary>
/// Stable error codes produced by the domain layer. Repositories throw
/// <see cref="DomainException"/> carrying one of these codes; services translate them into
/// business results; tools translate them into protocol responses. Autodesk exceptions never
/// cross the domain boundary un-wrapped.
/// </summary>
public enum DomainErrorCode
{
    /// <summary>No drawing/document is currently open in the host application.</summary>
    NoActiveDocument,

    /// <summary>A requested entity (alignment, surface, …) could not be found.</summary>
    EntityNotFound,

    /// <summary>An entity with the requested name already exists; rename rejected.</summary>
    DuplicateName,

    /// <summary>The requested name is invalid (empty, too long, or contains unsupported characters).</summary>
    InvalidName,

    /// <summary>No pipe network part (family or size) matches the requested criteria, or the match is ambiguous.</summary>
    PartNotFound,

    /// <summary>A read-only query against the Autodesk database failed.</summary>
    TransactionFailed,

    /// <summary>An unexpected internal failure occurred inside the domain layer.</summary>
    Internal,
}
