using System.Globalization;
using Civil3D.Domain.Commands;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Errors;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Pipes.Materials;
using Civil3D.Domain.Pipes.Repositories;

namespace Civil3D.Domain.Pipes.Services;

/// <summary>
/// Create-pipe orchestration for pipe networks: confirms the target network exists (via the
/// read repository, so a missing network fails with the standard <c>EntityNotFound</c> before any
/// Autodesk write is attempted), invokes the write repository inside the active transaction, and
/// raises the <c>PartCreated</c> domain event. Autodesk-free.
/// </summary>
public sealed class CreatePipeService : ICreatePipeService
{
    private readonly IPipeRepository _read;
    private readonly IPipeCreateRepository _write;
    private readonly IDomainEventDispatcher _events;

    /// <summary>Creates the service.</summary>
    /// <param name="read">The read-only pipe network repository.</param>
    /// <param name="write">The pipe create write repository.</param>
    /// <param name="events">The domain event dispatcher.</param>
    public CreatePipeService(IPipeRepository read, IPipeCreateRepository write, IDomainEventDispatcher events)
    {
        _read = read ?? throw new ArgumentNullException(nameof(read));
        _write = write ?? throw new ArgumentNullException(nameof(write));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    /// <inheritdoc />
    public CreatePipeResult Create(IWriteTransaction transaction, CreatePipeSpecification specification, ICommandExecutionContext context)
    {
        // 1. Material-aware rating validation: reject SDR/PN values that are not standard for the
        //    requested material before any document access.
        ValidateRating(specification);

        // 2. The target network must already exist; create_pipe never creates one implicitly.
        PipeNetworkInfo network = _read.GetByName(specification.NetworkName);

        // 3. Perform the Autodesk creation inside the active write transaction.
        CreatePipeOutcome outcome = _write.Create(transaction, network.Id, specification);

        // 3. Raise the domain event.
        _events.PublishAsync(
            new PartCreated(
                PartType: "pipe",
                PartId: outcome.PipeId,
                NetworkId: network.Id,
                Name: outcome.Name,
                CorrelationId: context.CorrelationId,
                SessionId: context.SessionId),
            context.CancellationToken).GetAwaiter().GetResult();

        return new CreatePipeResult
        {
            PipeId = outcome.PipeId,
            Name = outcome.Name,
            NetworkId = outcome.NetworkId,
            NetworkName = outcome.NetworkName,
            PartFamilyName = outcome.PartFamilyName,
            PartSizeName = outcome.PartSizeName,
            Material = outcome.Material,
            InnerDiameterOrWidth = outcome.InnerDiameterOrWidth,
            OuterDiameterOrWidth = outcome.OuterDiameterOrWidth,
            StartEasting = outcome.StartEasting,
            StartNorthing = outcome.StartNorthing,
            StartElevation = outcome.StartElevation,
            EndEasting = outcome.EndEasting,
            EndNorthing = outcome.EndNorthing,
            EndElevation = outcome.EndElevation,
            Length3D = outcome.Length3D,
            Success = true,
            TimestampUtc = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Rejects SDR/PN values that are not standard for the requested material — for example an
    /// SDR on a Ductile Iron pipe (rigid pipes are rated by PN pressure class only) or SDR 99 on
    /// HDPE. Unknown materials skip validation and keep the text-match resolution behaviour, so
    /// custom catalogs are unaffected.
    /// </summary>
    private static void ValidateRating(CreatePipeSpecification specification)
    {
        PipeMaterialInfo? material = PipeMaterials.Resolve(specification.Material);
        if (material is null)
        {
            return;
        }

        if (specification.Sdr is { } sdrText)
        {
            if (material.RatingMode != PipeRatingMode.SdrAndPn)
            {
                throw new DomainException(
                    DomainErrorCode.ValidationFailed,
                    material.RatingMode == PipeRatingMode.PressureClassOnly
                        ? $"{material.Name} pipes are rated by pressure class (PN), not SDR. Remove the SDR value."
                        : $"{material.Name} pipes have no SDR/PN rating. Remove the SDR value.");
            }

            if (!double.TryParse(sdrText, NumberStyles.Float, CultureInfo.InvariantCulture, out double sdrValue)
                || !material.SupportedSdrValues.Contains(sdrValue))
            {
                throw new DomainException(
                    DomainErrorCode.ValidationFailed,
                    $"'{sdrText}' is not a standard SDR for {material.Name} " +
                    $"(supported: {string.Join(", ", material.SupportedSdrValues)}).");
            }
        }

        if (specification.PressureClassBar is { } pressureClassBar)
        {
            if (material.RatingMode == PipeRatingMode.None)
            {
                throw new DomainException(
                    DomainErrorCode.ValidationFailed,
                    $"{material.Name} pipes have no PN pressure class. Remove the pressure class value.");
            }

            if (!material.SupportedPressureClassesBar.Contains(pressureClassBar))
            {
                throw new DomainException(
                    DomainErrorCode.ValidationFailed,
                    $"PN {pressureClassBar.ToString("0.#", CultureInfo.InvariantCulture)} is not a standard " +
                    $"pressure class for {material.Name} (supported: " +
                    $"{string.Join(", ", material.SupportedPressureClassesBar)}).");
            }
        }
    }
}
