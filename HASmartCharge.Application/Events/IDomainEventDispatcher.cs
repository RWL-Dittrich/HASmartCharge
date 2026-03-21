using HASmartCharge.Domain.Events;

namespace HASmartCharge.Application.Events;

/// <summary>Dispatches domain events to registered handlers.</summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct = default);
    Task DispatchAllAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default);
}
