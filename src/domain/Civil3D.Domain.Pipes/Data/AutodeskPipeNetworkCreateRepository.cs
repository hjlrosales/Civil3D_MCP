using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Data;
using Civil3D.Domain.Errors;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Pipes.Repositories;

namespace Civil3D.Domain.Pipes.Data;

/// <summary>
/// Real <see cref="IPipeNetworkCreateRepository"/>: creates (or reuses) the parts list, adds the
/// requested material pipe part families from the installed Civil 3D pipe catalog, populates each
/// added family with the requested nominal sizes (diameters), creates the network via
/// <c>Network.Create</c>, and assigns the parts list. Families are matched per material against
/// known catalog description variants (metric "… Pipe SI" and imperial "… Pipe"), so the same
/// request works in both unit systems; materials without a catalog family are reported in
/// <see cref="CreatePipeNetworkOutcome.FamiliesFailed"/> instead of failing the whole command.
/// </summary>
/// <remarks>
/// <c>PartsList.AddPartFamilyByDescription</c> adds the family shell with no sizes in current
/// Civil 3D versions, so each family is populated with explicit <see cref="SizeFilterRecord"/>
/// sizes (inner diameter in millimetres) before the parts list is assigned to the network.
/// </remarks>
public sealed class AutodeskPipeNetworkCreateRepository : IPipeNetworkCreateRepository
{
    private static readonly IReadOnlyDictionary<string, string[]> MaterialCatalogVariants =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["HDPE"] = ["HDPE Pipe SI", "HDPE Pipe"],
            ["PVC"] = ["PVC Pipe SI", "PVC Pipe"],
            ["Concrete"] = ["Concrete Pipe SI", "Concrete Pipe"],
            ["Ductile Iron"] = ["Ductile Iron Pipe SI", "Ductile Iron Pipe"],
            ["Corrugated HDPE"] = ["Corrugated HDPE Pipe SI", "Corrugated HDPE Pipe"],
            ["Corrugated Metal"] = ["Corrugated Metal Pipe SI", "Corrugated Metal Pipe"],
        };

    private readonly IAutodeskDocumentContext _context;

    /// <summary>Creates the repository over the document context.</summary>
    /// <param name="context">Resolves the active drawing database.</param>
    public AutodeskPipeNetworkCreateRepository(IAutodeskDocumentContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public CreatePipeNetworkOutcome Create(IWriteTransaction transaction, CreatePipeNetworkSpecification specification)
        => _context.ExecuteWrite(database => CreateCore((Database)database, transaction, specification));

    private static CreatePipeNetworkOutcome CreateCore(
        Database database, IWriteTransaction transaction, CreatePipeNetworkSpecification specification)
    {
        if (transaction.Handle is not Transaction tx)
        {
            throw new DomainException(
                DomainErrorCode.TransactionFailed,
                "The active transaction is not an AutoCAD database transaction.");
        }

        CivilDocument civilDocument = CivilDocument.GetCivilDocument(database);
        PartsListCollection partsLists = civilDocument.Styles.PartsListSet;

        string partsListName = string.IsNullOrWhiteSpace(specification.PartsListName)
            ? $"{specification.Name} Parts List"
            : specification.PartsListName.Trim();

        var familiesAdded = new List<string>();
        var familiesFailed = new List<string>();
        ObjectId partsListId = ObjectId.Null;

        if (partsLists.Contains(partsListName))
        {
            partsListId = partsLists[partsListName];
        }
        else
        {
            try
            {
                partsListId = partsLists.Add(partsListName);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                throw new DomainException(
                    DomainErrorCode.TransactionFailed,
                    $"Civil 3D could not create parts list '{partsListName}'.", ex);
            }

            var partsList = (PartsList)tx.GetObject(partsListId, OpenMode.ForWrite);
            foreach (string material in specification.Materials)
            {
                bool added = false;
                foreach (string variant in CatalogVariants(material))
                {
                    try
                    {
                        partsList.AddPartFamilyByDescription(DomainType.Pipe, variant);
                        familiesAdded.Add(variant);
                        added = true;
                        AddRequestedSizes(partsList, variant, specification.SizesMm);
                        break;
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception)
                    {
                        // The catalog for this drawing's units has no such family; try the next variant.
                    }
                }

                if (!added)
                {
                    familiesFailed.Add(material);
                }
            }
        }

        string networkName = specification.Name;
        ObjectId networkId;
        try
        {
            networkId = Network.Create(civilDocument, ref networkName);
        }
        catch (Autodesk.AutoCAD.Runtime.Exception ex)
        {
            throw new DomainException(
                DomainErrorCode.TransactionFailed,
                $"Civil 3D rejected the new pipe network '{specification.Name}'.", ex);
        }

        var network = (Network)tx.GetObject(networkId, OpenMode.ForWrite);
        if (!string.IsNullOrWhiteSpace(specification.Description))
        {
            network.Description = specification.Description;
        }

        if (!partsListId.IsNull)
        {
            network.PartsListId = partsListId;
        }

        return new CreatePipeNetworkOutcome
        {
            NetworkId = network.ObjectId.Handle.Value,
            Name = network.Name,
            PartsListName = partsListName,
            FamiliesAdded = familiesAdded,
            FamiliesFailed = familiesFailed,
        };
    }

    /// <summary>
    /// Adds a <see cref="SizeFilterRecord"/> size for every requested nominal inner diameter
    /// (millimetres) to the pipe part family whose description matches <paramref name="familyDescription"/>.
    /// The size's inner-diameter parameter is found by description; sizes outside the catalog's
    /// valid range are skipped silently (the family keeps whatever sizes it could take).
    /// </summary>
    private static void AddRequestedSizes(PartsList partsList, string familyDescription, IReadOnlyList<double> sizesMm)
    {
        if (sizesMm.Count == 0)
        {
            return;
        }

        foreach (ObjectId familyId in partsList.GetPartFamilyIdsByDomain(DomainType.Pipe))
        {
            var family = (PartFamily)familyId.GetObject(OpenMode.ForWrite);
            if (!family.Description.Equals(familyDescription, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (double diameterMm in sizesMm)
            {
                try
                {
                    var sizeRecord = new SizeFilterRecord(family);
                    SizeFilterField? diameterField = null;
                    for (int i = 0; i < sizeRecord.ParamCount; i++)
                    {
                        SizeFilterField field = sizeRecord[i];
                        string desc = (field.Description ?? string.Empty).ToLowerInvariant();
                        if (desc.Contains("inner pipe diameter") || desc.Contains("inner diameter"))
                        {
                            diameterField = field;
                            break;
                        }
                    }

                    if (diameterField is null)
                    {
                        continue;
                    }

                    diameterField.Value = diameterMm;
                    family.AddPartSize(sizeRecord);
                }
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                    // The diameter is outside the family's catalog range; keep the other sizes.
                }
            }
        }
    }

    /// <summary>
    /// Returns the catalog family-description variants to try for a material: the known map first,
    /// then a generic "&lt;material&gt; Pipe SI" / "&lt;material&gt; Pipe" fallback so unknown
    /// materials still resolve when the catalog names them that way.
    /// </summary>
    private static string[] CatalogVariants(string material)
    {
        string trimmed = material.Trim();
        if (MaterialCatalogVariants.TryGetValue(trimmed, out string[]? known))
        {
            return known;
        }

        return [$"{trimmed} Pipe SI", $"{trimmed} Pipe"];
    }
}
