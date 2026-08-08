namespace Civil3D.Domain.Query.Tests;

/// <summary>Classification of a sample widget.</summary>
public enum WidgetKind
{
    Road,
    Rail,
    Utility,
}

/// <summary>A representative DTO used to exercise the query engine.</summary>
public sealed record Widget(
    long Id,
    string Name,
    string? Description,
    double Length,
    WidgetKind Kind,
    int? Rank);
