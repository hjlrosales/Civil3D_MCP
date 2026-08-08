namespace Civil3D.Domain.Commands;

/// <summary>
/// A unit of undo created by <see cref="IUndoContext.Begin"/>. The real AutoCAD integration
/// (UndoRecord/UndoStack) is Phase 5B+; until then commands interact with this abstraction and
/// the no-op implementation is used. A unit must be committed or rolled back before disposal.
/// </summary>
public interface IUndoUnit : IDisposable
{
    /// <summary>Marks the unit as successful; makes it undoable in the host.</summary>
    void Commit();

    /// <summary>Discards the unit's undo record.</summary>
    void Rollback();
}

/// <summary>
/// Abstraction for the host's undo stack. Keeps command handlers free of AutoCAD undo APIs;
/// the bridge will provide a real implementation that opens an UndoRecord around the command's
/// transaction.
/// </summary>
public interface IUndoContext
{
    /// <summary>Begins a new undo unit for the named operation.</summary>
    /// <param name="description">Human-readable description shown in the undo menu.</param>
    IUndoUnit Begin(string description);
}

/// <summary>A no-op <see cref="IUndoContext"/> used until AutoCAD undo integration lands.</summary>
public sealed class NullUndoContext : IUndoContext
{
    private NullUndoContext() { }

    /// <summary>The shared instance.</summary>
    public static NullUndoContext Instance { get; } = new();

    /// <inheritdoc />
    public IUndoUnit Begin(string description) => NullUndoUnit.Instance;

    private sealed class NullUndoUnit : IUndoUnit
    {
        internal static NullUndoUnit Instance { get; } = new();

        private NullUndoUnit() { }

        public void Commit() { }
        public void Rollback() { }
        public void Dispose() { }
    }
}
