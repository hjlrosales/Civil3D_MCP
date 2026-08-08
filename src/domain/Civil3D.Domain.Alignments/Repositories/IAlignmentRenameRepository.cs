using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Dtos;

namespace Civil3D.Domain.Alignments.Repositories;

/// <summary>
/// Write seam for renaming alignments. The pipeline hands the active
/// <see cref="IWriteTransaction"/> (already begun and document-locked) and commits it after the
/// rename; the Autodesk implementation casts the transaction handle and sets the alignment name.
/// Throws <see cref="Civil3D.Domain.Errors.DomainException"/> on failure (entity not found,
/// wrong object type, transaction failure).
/// </summary>
public interface IAlignmentRenameRepository
{
    /// <summary>Renames the alignment with the given id to <paramref name="newName"/>.</summary>
    /// <param name="transaction">The active write transaction.</param>
    /// <param name="id">Stable numeric id of the alignment.</param>
    /// <param name="newName">The new alignment name.</param>
    RenameOutcome Rename(IWriteTransaction transaction, long id, string newName);
}
