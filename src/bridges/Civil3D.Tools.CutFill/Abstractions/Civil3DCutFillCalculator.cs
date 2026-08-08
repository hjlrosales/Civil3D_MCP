using Microsoft.Extensions.Logging;

namespace Civil3D.Tools.CutFill.Abstractions;

/// <summary>
/// Production implementation of <see cref="ICutFillCalculator"/> for the Civil 3D host.
///
/// The Civil 3D managed API exposes volume data only through volume surfaces
/// (<c>TinVolumeSurface</c>/<c>GridVolumeSurface</c>), which must be created inside a document
/// write transaction and added to the drawing database before their statistics become readable.
/// That is a drawing modification, which the read-only workflow must not perform, and the
/// current domain layer exposes no read-only volume path. Per the platform availability rule,
/// this limitation is isolated here: the calculator returns a structured
/// <see cref="CutFillStatus.NotSupported"/> result with the reason, instead of inventing API
/// behavior. A future phase can replace this class with a real engine behind the same
/// <see cref="ICutFillCalculator"/> contract — the workflow and tools do not change.
/// </summary>
public sealed class Civil3DCutFillCalculator : ICutFillCalculator
{
    private readonly ILogger<Civil3DCutFillCalculator> _logger;

    /// <summary>Creates the calculator.</summary>
    /// <param name="logger">Used to record the not-supported decision.</param>
    public Civil3DCutFillCalculator(ILogger<Civil3DCutFillCalculator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public CutFillCalculationResult Calculate(CutFillCalculationData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        const string reason =
            "Read-only cut/fill volumes are not supported by the current Civil 3D API surface: "
            + "volume statistics require creating a volume surface in a document write transaction, "
            + "which the read-only workflow must not perform.";

        _logger.LogInformation(
            "Cut/fill calculation not supported: {Reason} (surfaces {ExistingId} vs {ProposedId}).",
            reason, data.ExistingSurface.Id, data.ProposedSurface.Id);

        return new CutFillCalculationResult
        {
            Status = CutFillStatus.NotSupported,
            NotSupportedReason = reason,
        };
    }
}
