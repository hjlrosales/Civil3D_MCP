using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Civil3D.Bridge.Diagnostics;
using Civil3D.Domain.Data;
using Civil3D.Domain.Errors;
using CoreApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Civil3D.Bridge.Data;

/// <summary>
/// Real <see cref="IAutodeskDocumentContext"/>: resolves the active drawing from the document
/// manager and hands its <see cref="Database"/> to the read delegate. Throws
/// <c>DomainException(NoActiveDocument)</c> when no drawing is open and maps every other Autodesk
/// failure to <c>TransactionFailed</c>, so raw Autodesk exceptions never escape the domain layer.
/// Must only be invoked on the application context (the tool dispatcher guarantees this).
/// </summary>
public sealed class AutodeskDocumentContext : IAutodeskDocumentContext
{
    /// <inheritdoc />
    public bool HasActiveDocument
        => CoreApplication.DocumentManager.MdiActiveDocument is not null;

    /// <inheritdoc />
    public T ExecuteRead<T>(Func<object, T> read, CancellationToken cancellationToken = default)
    {
        Diag.Log("ExecuteRead: MdiActiveDocument access");
        Document? document = CoreApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            throw new DomainException(
                DomainErrorCode.NoActiveDocument,
                "No active document is available to operate on.");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            T result = read(document.Database);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch (DomainException)
        {
            throw; // Stable domain code already chosen; never remap.
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DomainException(
                DomainErrorCode.TransactionFailed,
                "The read-only query against the drawing database failed.",
                ex);
        }
    }

    /// <inheritdoc />
    public T ExecuteWrite<T>(Func<object, T> write, CancellationToken cancellationToken = default)
    {
        Document? document = CoreApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            throw new DomainException(
                DomainErrorCode.NoActiveDocument,
                "No active document is available to operate on.");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            T result = write(document.Database);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch (DomainException)
        {
            throw; // Stable domain code already chosen; never remap.
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DomainException(
                DomainErrorCode.TransactionFailed,
                "The write operation against the drawing database failed.",
                ex);
        }
    }
}
