namespace Civil3D.Domain.Query;

/// <summary>
/// The closed set of comparison operators a filter can use. Only these operators are supported;
/// arbitrary expressions are deliberately not implemented.
/// </summary>
public enum FilterOperator
{
    /// <summary>Property equals the value (string comparisons are case-insensitive).</summary>
    Equals,

    /// <summary>Property is not equal to the value.</summary>
    NotEquals,

    /// <summary>String property contains the value (case-insensitive).</summary>
    Contains,

    /// <summary>String property starts with the value (case-insensitive).</summary>
    StartsWith,

    /// <summary>String property ends with the value (case-insensitive).</summary>
    EndsWith,

    /// <summary>Orderable property is greater than the value.</summary>
    GreaterThan,

    /// <summary>Orderable property is greater than or equal to the value.</summary>
    GreaterThanOrEqual,

    /// <summary>Orderable property is less than the value.</summary>
    LessThan,

    /// <summary>Orderable property is less than or equal to the value.</summary>
    LessThanOrEqual,

    /// <summary>Property is one of the listed values.</summary>
    In,

    /// <summary>Property is not one of the listed values.</summary>
    NotIn,

    /// <summary>Property is null.</summary>
    IsNull,

    /// <summary>Property is not null.</summary>
    IsNotNull,
}
