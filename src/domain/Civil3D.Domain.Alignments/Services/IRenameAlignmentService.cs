using Civil3D.Domain.Commands;
using Civil3D.Domain.Commands.Transactions;

namespace Civil3D.Domain.Alignments.Services;

/// <summary>
/// Rename orchestration for alignments: validates the target exists, rejects no-op renames and
/// duplicate names, invokes the write repository inside the active transaction, and raises the
/// <c>ObjectRenamed</c> domain event. Autodesk-free.
/// </summary>
public interface IRenameAlignmentService
{
    /// <summary>Renames the alignment with the given id inside the active write transaction.</summary>
    /// <param name="transaction">The active write transaction (document-locked).</param>
    /// <param name="id">Stable numeric id of the alignment.</param>
    /// <param name="newName">The new alignment name.</param>
    /// <param name="context">Per-command execution context.</param>
    RenameResult Rename(IWriteTransaction transaction, long id, string newName, ICommandExecutionContext context);
}
