namespace Civil3D.Tools.Quantity.Dtos;

/// <summary>
/// The unit of measure of a quantity line item. Kept to a small closed set so the report stays
/// serializable and machine-readable; future phases that add area/volume metrics extend this
/// enum (and the calculation engine) together.
/// </summary>
public enum QuantityUnit
{
    /// <summary>A count of objects; the default unit.</summary>
    Count = 0,

    /// <summary>A linear measure in drawing units.</summary>
    Length = 1,

    /// <summary>A binary measure of file size in bytes.</summary>
    Bytes = 2,
}
