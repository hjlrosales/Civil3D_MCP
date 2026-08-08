using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Dtos;

namespace Civil3D.Domain.Surfaces.Repositories;

/// <summary>
/// Write seam for renaming surfaces. The pipeline hands the active
/// <see cref="IWriteTransaction"/> (already begun and document-locked) and commits it after the
/// rename; the Autodesk implementation casts the transaction handle and sets the surface name.
/// Throws <see cref="Civil3D.Domain.Errors.DomainException"/> on failure.
/// </summary>
public interface ISurfaceRenameRepository
{
    /// <summary>Renames the surface with the given id to <paramref name="newName"/>.</summary>
    /// <param name="transaction">The active write transaction.</param>
    /// <param name="id">Stable numeric id of the surface.</param>
    /// <param name="newName">The new surface name.</param>
    RenameOutcome Rename(IWriteTransaction transaction, long id, string newName);
}
