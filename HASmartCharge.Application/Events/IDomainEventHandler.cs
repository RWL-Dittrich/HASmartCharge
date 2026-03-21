using HASmartCharge.Domain.Events;

namespace HASmartCharge.Application.Events;

/// <summary>Handles a specific domain event type.</summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct = default);
}
