namespace Civil3D.Tools.CutFill.Abstractions;

/// <summary>
/// Abstraction over the earthwork volume engine. The workflow depends only on this contract;
/// the Autodesk-backed production implementation (<see cref="Civil3DCutFillCalculator"/>) and
/// any test double or future engine implement it. Implementations must never throw for
/// unavailable APIs — they return a structured <see cref="CutFillCalculationResult"/> with
/// <see cref="CutFillStatus.NotSupported"/> instead.
/// </summary>
public interface ICutFillCalculator
{
    /// <summary>Calculates the cut/fill volumes between the two surfaces in the snapshot.</summary>
    /// <param name="data">The loaded surfaces and thresholds.</param>
    CutFillCalculationResult Calculate(CutFillCalculationData data);
}
