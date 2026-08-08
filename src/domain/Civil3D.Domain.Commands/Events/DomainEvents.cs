namespace Civil3D.Domain.Commands;

/// <summary>Published when the dispatcher starts executing a command.</summary>
/// <param name="CommandName">The command name.</param>
/// <param name="CorrelationId">Correlation of the originating request.</param>
/// <param name="SessionId">Session of the originating request, when present.</param>
public sealed record CommandStarted(string CommandName, string CorrelationId, string? SessionId) : IDomainEvent;

/// <summary>Published when a command completed (committed or read-only).</summary>
/// <param name="CommandName">The command name.</param>
/// <param name="CorrelationId">Correlation of the originating request.</param>
/// <param name="SessionId">Session of the originating request, when present.</param>
/// <param name="ExecutionTimeMs">Wall-clock execution time of the command.</param>
/// <param name="Committed">True when a write transaction was committed.</param>
public sealed record CommandCompleted(string CommandName, string CorrelationId, string? SessionId, long ExecutionTimeMs, bool Committed) : IDomainEvent;

/// <summary>Published when a command failed. The reason never carries exception details.</summary>
/// <param name="CommandName">The command name.</param>
/// <param name="CorrelationId">Correlation of the originating request.</param>
/// <param name="SessionId">Session of the originating request, when present.</param>
/// <param name="ErrorCode">The stable <see cref="CommandErrorCode"/> name.</param>
/// <param name="RollbackReason">The transaction rollback reason, when a transaction was rolled back.</param>
public sealed record CommandFailed(string CommandName, string CorrelationId, string? SessionId, string ErrorCode, string? RollbackReason) : IDomainEvent;

/// <summary>Published after a write transaction commits.</summary>
/// <param name="CommandName">The command name.</param>
/// <param name="CorrelationId">Correlation of the originating request.</param>
/// <param name="Duration">The transaction duration.</param>
public sealed record TransactionCommitted(string CommandName, string CorrelationId, TimeSpan Duration) : IDomainEvent;

/// <summary>Published after a write transaction rolls back, with the reason.</summary>
/// <param name="CommandName">The command name.</param>
/// <param name="CorrelationId">Correlation of the originating request.</param>
/// <param name="Reason">Why the transaction was rolled back (timeout, cancelled, exception type name).</param>
/// <param name="Duration">The transaction duration before rollback.</param>
public sealed record TransactionRolledBack(string CommandName, string CorrelationId, string Reason, TimeSpan Duration) : IDomainEvent;

/// <summary>Published after an object was renamed in a committed write transaction.</summary>
/// <param name="ObjectType">The discipline/entity kind (for example "alignment" or "surface").</param>
/// <param name="ObjectId">Stable numeric id of the renamed object.</param>
/// <param name="PreviousName">The name before the rename.</param>
/// <param name="NewName">The name after the rename.</param>
/// <param name="CorrelationId">Correlation of the originating request.</param>
/// <param name="SessionId">Session of the originating request, when present.</param>
public sealed record ObjectRenamed(string ObjectType, long ObjectId, string PreviousName, string NewName, string CorrelationId, string? SessionId) : IDomainEvent;
