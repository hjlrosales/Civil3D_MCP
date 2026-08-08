namespace Civil3D.Tools.Project.Dtos;

/// <summary>
/// The priority of a project recommendation, ordered from lowest to highest.
/// </summary>
public enum RecommendationPriority
{
    /// <summary>Nice-to-have cleanup; no immediate impact.</summary>
    Low = 0,

    /// <summary>Worth addressing during normal maintenance.</summary>
    Medium = 1,

    /// <summary>Should be addressed before further work.</summary>
    High = 2,

    /// <summary>Address immediately; likely to affect production use.</summary>
    Critical = 3,
}
