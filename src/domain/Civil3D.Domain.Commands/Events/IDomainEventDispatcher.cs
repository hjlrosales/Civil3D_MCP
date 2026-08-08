namespace Civil3D.Domain.Commands;

/// <summary>Publishes domain events to any registered subscribers.</summary>
public interface IDomainEventDispatcher
{
    /// <summary>Publishes an event to all subscribers.</summary>
    /// <param name="domainEvent">The event to publish.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory <see cref="IDomainEventDispatcher"/> with no subscribers required (Phase 5A). All
/// published events are retained in <see cref="Published"/> for observability and tests; future
/// phases may add subscriber hooks. Thread-safe.
/// </summary>
public sealed class InMemoryDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly object _sync = new();
    private readonly List<IDomainEvent> _published = [];

    /// <summary>All events published so far, in order.</summary>
    public IReadOnlyList<IDomainEvent> Published
    {
        get
        {
            lock (_sync)
            {
                return _published.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            _published.Add(domainEvent);
        }

        return Task.CompletedTask;
    }
}
