using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Data;
using Civil3D.Domain.Errors;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Pipes.Repositories;

namespace Civil3D.Domain.Pipes.Data;

/// <summary>
/// Real <see cref="IPipeUpdateRepository"/>: opens the pipe by its stable numeric id (its
/// database handle) for write inside the active transaction and applies the requested changes —
/// elevation of both ends, horizontal length along the pipe's current bearing (start fixed, end
/// moved), and resizing to the part size closest to a requested inner diameter via
/// <c>Pipe.ResizeByInnerDiameterOrWidth</c>. The change list and the full pipe state are read
/// back into the immutable outcome.
/// </summary>
public sealed class AutodeskPipeUpdateRepository : IPipeUpdateRepository
{
    private readonly IAutodeskDocumentContext _context;

    /// <summary>Creates the repository over the document context.</summary>
    /// <param name="context">Resolves the active drawing database.</param>
    public AutodeskPipeUpdateRepository(IAutodeskDocumentContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public UpdatePipeOutcome Update(IWriteTransaction transaction, UpdatePipeSpecification specification)
        => _context.ExecuteWrite(
            database => UpdateCore((Database)database, transaction, specification));

    private static UpdatePipeOutcome UpdateCore(
        Database database, IWriteTransaction transaction, UpdatePipeSpecification specification)
    {
        if (transaction.Handle is not Transaction tx)
        {
            throw new DomainException(
                DomainErrorCode.TransactionFailed,
                "The active transaction is not an AutoCAD database transaction.");
        }

        Pipe pipe = OpenPipeForWrite(database, tx, specification.PipeId);
        var changes = new List<string>();

        Point3d start = pipe.StartPoint;
        Point3d end = pipe.EndPoint;

        // 1. Elevation: both ends move to the requested elevation (the pipe stays horizontal).
        if (specification.ElevationMeters is { } elevation)
        {
            start = new Point3d(start.X, start.Y, elevation);
            end = new Point3d(end.X, end.Y, elevation);
            changes.Add("elevation");
        }

        // 2. Length: keep the start fixed and move the end along the pipe's current horizontal
        //    bearing; the end elevation (possibly just updated above) is preserved.
        if (specification.LengthMeters is { } length)
        {
            double deltaX = end.X - start.X;
            double deltaY = end.Y - start.Y;
            double horizontal = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            if (horizontal <= 1e-9)
            {
                throw new DomainException(
                    DomainErrorCode.ValidationFailed,
                    $"Pipe '{pipe.Name}' has no horizontal direction; its length cannot be rescaled.");
            }

            double unitX = deltaX / horizontal;
            double unitY = deltaY / horizontal;
            end = new Point3d(start.X + (unitX * length), start.Y + (unitY * length), end.Z);
            changes.Add("length");
        }

        try
        {
            pipe.StartPoint = start;
            pipe.EndPoint = end;
        }
        catch (Autodesk.AutoCAD.Runtime.Exception ex)
        {
            throw new DomainException(
                DomainErrorCode.TransactionFailed,
                $"Civil 3D rejected the new geometry for pipe '{pipe.Name}'.", ex);
        }

        // 3. Diameter: resize to the available part size closest to the requested inner diameter.
        if (specification.DiameterMm is { } diameterMm)
        {
            try
            {
                pipe.ResizeByInnerDiameterOrWidth(diameterMm, useClosestSize: true);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                throw new DomainException(
                    DomainErrorCode.TransactionFailed,
                    $"Civil 3D rejected the diameter change for pipe '{pipe.Name}' (requested {diameterMm:0.#} mm).", ex);
            }

            changes.Add("diameter");
        }

        return new UpdatePipeOutcome
        {
            PipeId = pipe.ObjectId.Handle.Value,
            Name = pipe.Name,
            NetworkId = pipe.NetworkId.Handle.Value,
            NetworkName = pipe.NetworkName,
            PartFamilyName = pipe.PartFamilyName,
            PartSizeName = pipe.PartSizeName,
            Material = string.IsNullOrWhiteSpace(pipe.Material) ? null : pipe.Material,
            InnerDiameterOrWidth = pipe.InnerDiameterOrWidth,
            OuterDiameterOrWidth = pipe.OuterDiameterOrWidth,
            StartEasting = pipe.StartPoint.X,
            StartNorthing = pipe.StartPoint.Y,
            StartElevation = pipe.StartPoint.Z,
            EndEasting = pipe.EndPoint.X,
            EndNorthing = pipe.EndPoint.Y,
            EndElevation = pipe.EndPoint.Z,
            Length3D = pipe.Length3D,
            ChangesApplied = changes,
        };
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
