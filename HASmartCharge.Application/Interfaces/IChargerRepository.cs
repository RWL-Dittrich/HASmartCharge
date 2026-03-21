using HASmartCharge.Domain.Entities;

namespace HASmartCharge.Application.Interfaces;

/// <summary>Repository for charger aggregates.</summary>
public interface IChargerRepository
{
    Task<Charger?> GetByIdAsync(string chargePointId, CancellationToken ct = default);
    Task<IReadOnlyList<Charger>> GetAllAsync(CancellationToken ct = default);
    Task SaveAsync(Charger charger, CancellationToken ct = default);
}
