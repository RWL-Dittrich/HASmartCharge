namespace HASmartCharge.Application.Interfaces;

/// <summary>
/// Outbound charger operations expressed in business/capability terms,
/// decoupled from the underlying OCPP protocol.
/// </summary>
public interface IChargerGateway
{
    Task<ChargerCommandResult> ResetChargerAsync(string chargerId, bool hardReset, CancellationToken ct = default);
    Task<ChargerCommandResult> ClearCacheAsync(string chargerId, CancellationToken ct = default);
    Task<ChargerCommandResult> TriggerMessageAsync(string chargerId, string requestedMessage, int? connectorId, CancellationToken ct = default);
    Task<ChargerCommandResult> GetDiagnosticsAsync(string chargerId, string location, CancellationToken ct = default);
    Task<ChargerCommandResult> SetConnectorAvailabilityAsync(string chargerId, int connectorId, bool available, CancellationToken ct = default);
    Task<ChargerCommandResult> UnlockConnectorAsync(string chargerId, int connectorId, CancellationToken ct = default);
    Task<ChargerCommandResult> StartTransactionAsync(string chargerId, int connectorId, string idTag, CancellationToken ct = default);
    Task<ChargerCommandResult> StopTransactionAsync(string chargerId, int transactionId, CancellationToken ct = default);
}
