using Civil3D.Domain.Profiles.Dtos;

namespace Civil3D.Domain.Profiles.Data;

/// <summary>
/// The Autodesk seam for the profiles discipline. Reads every profile once inside a single
/// read-only transaction (alignments are enumerated once, their profiles once) and returns
/// immutable DTOs; never exposes Autodesk objects.
/// </summary>
public interface IProfileDataSource
{
    /// <summary>Reads all profiles in the active drawing exactly once and materializes them.</summary>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    ProfileCollection ReadAll(CancellationToken cancellationToken = default);
}
