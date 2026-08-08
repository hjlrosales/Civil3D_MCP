namespace Civil3D.Tools.CutFill.Dtos;

/// <summary>
/// Optional derived statistics of a computed earthwork result; null when volumes were not
/// supported or statistics were disabled.
/// </summary>
public sealed record VolumeStatistics
{
    /// <summary>Cut volume as a percentage of the total (cut + fill); 0 when total is zero.</summary>
    public double CutPercentOfTotal { get; init; }

    /// <summary>Fill volume as a percentage of the total (cut + fill); 0 when total is zero.</summary>
    public double FillPercentOfTotal { get; init; }

    /// <summary>Signed net volume as a percentage of the total; 0 when total is zero.</summary>
    public double NetPercentOfTotal { get; init; }

    /// <summary>Cut ÷ fill ratio; 0 when fill is zero.</summary>
    public double CutFillRatio { get; init; }
}
