namespace Civil3D.Tools.Query.Dtos;

/// <summary>
/// The input of every <c>get_*</c> lookup tool: the stable numeric id of the object to return.
/// </summary>
public sealed record IdRequest
{
    /// <summary>The stable numeric id (database handle value) of the object.</summary>
    public long Id { get; init; }
}
