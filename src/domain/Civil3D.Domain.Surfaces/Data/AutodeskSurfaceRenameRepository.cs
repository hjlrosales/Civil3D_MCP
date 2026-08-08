using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Data;
using Civil3D.Domain.Dtos;
using Civil3D.Domain.Errors;
using Civil3D.Domain.Surfaces.Repositories;
using CivilSurface = Autodesk.Civil.DatabaseServices.Surface;

namespace Civil3D.Domain.Surfaces.Data;

/// <summary>
/// Real <see cref="ISurfaceRenameRepository"/>: performs the Autodesk rename inside the active
/// write transaction. The transaction handle is the Autodesk <see cref="Transaction"/>; the
/// surface is opened for write and its <c>Name</c> is set. No business rules here — the rename
/// service validates before this is called.
/// </summary>
public sealed class AutodeskSurfaceRenameRepository : ISurfaceRenameRepository
{
    private readonly IAutodeskDocumentContext _context;

    /// <summary>Creates the repository over the document context.</summary>
    /// <param name="context">Resolves the active drawing database.</param>
    public AutodeskSurfaceRenameRepository(IAutodeskDocumentContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public RenameOutcome Rename(IWriteTransaction transaction, long id, string newName)
        => _context.ExecuteWrite(
            database => RenameCore((Database)database, transaction, id, newName));

    private static RenameOutcome RenameCore(Database database, IWriteTransaction transaction, long id, string newName)
    {
        if (transaction.Handle is not Transaction tx)
        {
            throw new DomainException(
                DomainErrorCode.TransactionFailed,
                "The active transaction is not an AutoCAD database transaction.");
        }

        try
        {
            ObjectId objectId = database.GetObjectId(false, new Handle(id), 0);
            var surface = (CivilSurface)tx.GetObject(objectId, OpenMode.ForWrite);
            string previousName = surface.Name;
            surface.Name = newName;
            return new RenameOutcome(surface.ObjectId.Handle.Value, previousName, surface.Name);
        }
        catch (Autodesk.AutoCAD.Runtime.Exception ex)
        {
            throw new DomainException(
                DomainErrorCode.EntityNotFound,
                $"The surface with id {id} could not be opened for rename.",
                ex);
        }
    }
}
