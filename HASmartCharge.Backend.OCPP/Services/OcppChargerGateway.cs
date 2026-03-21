using HASmartCharge.Application.Interfaces;
using HASmartCharge.Backend.OCPP.Domain;
using HASmartCharge.Backend.OCPP.Models;

namespace HASmartCharge.Backend.OCPP.Services;

public sealed class OcppChargerGateway : IChargerGateway
{
    private readonly ISessionManager _sessionManager;

    public OcppChargerGateway(ISessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    private ChargerCommandResult? TryGetActiveSession(string chargerId, out IChargePointSession? session)
    {
        session = _sessionManager.GetByChargePointId(chargerId);
        if (session is null) return ChargerCommandResult.ChargerNotFound();
        if (!session.IsActive) return ChargerCommandResult.ChargerOffline();
        return null;
    }

    private static ChargerCommandResult Map(OcppCommandResult result) =>
        result.Success
            ? ChargerCommandResult.Succeeded(result.RawPayload)
            : ChargerCommandResult.Failed(result.ErrorCode, result.ErrorDescription, result.RawPayload);

    public async Task<ChargerCommandResult> ResetChargerAsync(string chargerId, bool hardReset, CancellationToken ct = default)
    {
        var err = TryGetActiveSession(chargerId, out var session);
        if (err is not null) return err;
        var result = await session!.SendCommandAsync("Reset", new ResetRequest { Type = hardReset ? "Hard" : "Soft" }, ct);
        return Map(result);
    }

    public async Task<ChargerCommandResult> ClearCacheAsync(string chargerId, CancellationToken ct = default)
    {
        var err = TryGetActiveSession(chargerId, out var session);
        if (err is not null) return err;
        var result = await session!.SendCommandAsync("ClearCache", new { }, ct);
        return Map(result);
    }

    public async Task<ChargerCommandResult> TriggerMessageAsync(string chargerId, string requestedMessage, int? connectorId, CancellationToken ct = default)
    {
        var err = TryGetActiveSession(chargerId, out var session);
        if (err is not null) return err;
        var result = await session!.SendCommandAsync("TriggerMessage", new TriggerMessageRequest { RequestedMessage = requestedMessage, ConnectorId = connectorId }, ct);
        return Map(result);
    }

    public async Task<ChargerCommandResult> GetDiagnosticsAsync(string chargerId, string location, CancellationToken ct = default)
    {
        var err = TryGetActiveSession(chargerId, out var session);
        if (err is not null) return err;
        var result = await session!.SendCommandAsync("GetDiagnostics", new GetDiagnosticsRequest { Location = location }, ct);
        return Map(result);
    }

    public async Task<ChargerCommandResult> SetConnectorAvailabilityAsync(string chargerId, int connectorId, bool available, CancellationToken ct = default)
    {
        var err = TryGetActiveSession(chargerId, out var session);
        if (err is not null) return err;
        var result = await session!.SetAvailabilityAsync(connectorId, available, ct);
        return Map(result);
    }

    public async Task<ChargerCommandResult> UnlockConnectorAsync(string chargerId, int connectorId, CancellationToken ct = default)
    {
        var err = TryGetActiveSession(chargerId, out var session);
        if (err is not null) return err;
        var result = await session!.SendCommandAsync("UnlockConnector", new UnlockConnectorRequest { ConnectorId = connectorId }, ct);
        return Map(result);
    }

    public async Task<ChargerCommandResult> StartTransactionAsync(string chargerId, int connectorId, string idTag, CancellationToken ct = default)
    {
        var err = TryGetActiveSession(chargerId, out var session);
        if (err is not null) return err;
        var result = await session!.RemoteStartTransactionAsync(connectorId, idTag, ct);
        return Map(result);
    }

    public async Task<ChargerCommandResult> StopTransactionAsync(string chargerId, int transactionId, CancellationToken ct = default)
    {
        var err = TryGetActiveSession(chargerId, out var session);
        if (err is not null) return err;
        var result = await session!.RemoteStopTransactionAsync(transactionId, ct);
        return Map(result);
    }
}
