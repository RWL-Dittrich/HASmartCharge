using HASmartCharge.Application.Queries.Models;

namespace HASmartCharge.Application.Interfaces;

/// <summary>
/// Provides protocol-agnostic charger and dashboard read access for the API layer.
/// </summary>
public interface IChargerReadModel
{
    /// <summary>
    /// Returns charger snapshots, optionally filtered by current connectivity.
    /// </summary>
    /// <param name="connected">
    /// <c>true</c> for connected chargers only, <c>false</c> for disconnected chargers only,
    /// or <c>null</c> to return all known chargers.
    /// </param>
    IEnumerable<ChargerSnapshot> GetChargers(bool? connected = null);

    /// <summary>
    /// Returns a full charger snapshot, including connector state and latest measurements.
    /// </summary>
    ChargerSnapshot? GetCharger(string chargerId);

    /// <summary>
    /// Returns active charging-session snapshots across all chargers or for a single charger.
    /// </summary>
    IEnumerable<ActiveChargingSessionSnapshot> GetActiveChargingSessions(string? chargerId = null);
}
