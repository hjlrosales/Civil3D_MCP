namespace Civil3D.Domain.Dtos;

/// <summary>
/// The outcome of a rename operation performed by a write repository: the object id plus the
/// previous and current names. Autodesk-free; produced inside the write transaction.
/// </summary>
public sealed record RenameOutcome(long ObjectId, string PreviousName, string CurrentName);
