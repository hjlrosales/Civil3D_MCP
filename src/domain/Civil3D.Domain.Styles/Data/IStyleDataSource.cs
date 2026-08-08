using Civil3D.Domain.Styles.Dtos;

namespace Civil3D.Domain.Styles.Data;

/// <summary>
/// The Autodesk seam for the styles discipline. Reads every style once inside a single read-only
/// transaction (enumerating the style collections under <c>StylesRoot</c>) and returns immutable
/// DTOs; never exposes Autodesk objects.
/// </summary>
public interface IStyleDataSource
{
    /// <summary>Reads all styles in the active drawing exactly once and materializes them.</summary>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    StyleCollection ReadAll(CancellationToken cancellationToken = default);
}
