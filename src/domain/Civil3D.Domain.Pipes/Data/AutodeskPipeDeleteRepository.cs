using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Data;
using Civil3D.Domain.Errors;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Pipes.Repositories;

namespace Civil3D.Domain.Pipes.Data;

/// <summary>
/// Real <see cref="IPipeDeleteRepository"/>: opens the pipe by its stable numeric id (its
/// database handle) for write inside the active transaction, reads its identity back (name,
/// owning network, part family and size), and erases it via <c>Pipe.Erase()</c> — the standard
/// removal path for a network pipe (the Civil 3D <c>Network</c> API has no dedicated
/// delete-pipe method). The deletion happens inside the transaction, so a later failure rolls it
/// back atomically.
/// </summary>
public sealed class AutodeskPipeDeleteRepository : IPipeDeleteRepository
{
    private readonly IAutodeskDocumentContext _context;

    /// <summary>Creates the repository over the document context.</summary>
    /// <param name="context">Resolves the active drawing database.</param>
    public AutodeskPipeDeleteRepository(IAutodeskDocumentContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public DeletePipeOutcome Delete(IWriteTransaction transaction, DeletePipeSpecification specification)
        => _context.ExecuteWrite(
            database => DeleteCore((Database)database, transaction, specification));

    private static DeletePipeOutcome DeleteCore(
        Database database, IWriteTransaction transaction, DeletePipeSpecification specification)
    {
        if (transaction.Handle is not Transaction tx)
        {
            throw new DomainException(
                DomainErrorCode.TransactionFailed,
                "The active transaction is not an AutoCAD database transaction.");
        }

        Pipe pipe = OpenPipeForWrite(database, tx, specification.PipeId);

        // Read the identity back before the pipe is erased, so the outcome can tell the caller
        // exactly what was removed.
        var outcome = new DeletePipeOutcome
        {
            PipeId = pipe.ObjectId.Handle.Value,
            Name = pipe.Name,
            NetworkId = pipe.NetworkId.Handle.Value,
            NetworkName = pipe.NetworkName,
            PartFamilyName = pipe.PartFamilyName,
            PartSizeName = pipe.PartSizeName,
        };

        try
        {
            pipe.Erase();
        }
        catch (Autodesk.AutoCAD.Runtime.Exception ex)
        {
            throw new DomainException(
                DomainErrorCode.TransactionFailed,
                $"Civil 3D rejected the deletion of pipe '{pipe.Name}' (id {specification.PipeId}).", ex);
        }

        return outcome;
    }

    private static Pipe OpenPipeForWrite(Database database, Transaction tx, long pipeId)
    {
        ObjectId pipeObjectId;
        try
        {
            pipeObjectId = database.GetObjectId(false, new Handle(pipeId), 0);
        }
        catch (Autodesk.AutoCAD.Runtime.Exception ex)
        {
            throw new DomainException(
                DomainErrorCode.EntityNotFound,
                $"No pipe with id {pipeId} could be resolved in the drawing.", ex);
        }

        try
        {
            return (Pipe)tx.GetObject(pipeObjectId, OpenMode.ForWrite);
        }
        catch (Autodesk.AutoCAD.Runtime.Exception ex)
        {
            throw new DomainException(
                DomainErrorCode.EntityNotFound,
                $"The pipe with id {pipeId} could not be opened for write (it may have been deleted).", ex);
        }
    }
}
