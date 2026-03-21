using HASmartCharge.Domain.Events;

namespace HASmartCharge.Application.Events;

/// <summary>
/// Simple in-process event dispatcher. Handlers are registered via <see cref="Register{TEvent}"/>.
/// Intended for DI registration as a singleton with handlers registered at startup.
/// </summary>
public sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly Dictionary<Type, List<Func<IDomainEvent, CancellationToken, Task>>> _handlers = [];

    public void Register<TEvent>(IDomainEventHandler<TEvent> handler) where TEvent : IDomainEvent
    {
        var type = typeof(TEvent);
        if (!_handlers.TryGetValue(type, out var list))
        {
            list = [];
            _handlers[type] = list;
        }
        list.Add((evt, ct) => handler.HandleAsync((TEvent)evt, ct));
    }

    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        if (_handlers.TryGetValue(domainEvent.GetType(), out var handlers))
            foreach (var handler in handlers)
                await handler(domainEvent, ct);
    }

    public async Task DispatchAllAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default)
    {
        foreach (var domainEvent in events)
            await DispatchAsync(domainEvent, ct);
    }
}
