using System.Globalization;
using HASmartCharge.Backend.OCPP.Models;
using HASmartCharge.Backend.OCPP.Services;

namespace HASmartCharge.Backend.Services.Telemetry;

/// <summary>
/// Implements the OCPP-shaped <see cref="IChargerTelemetrySink"/> (Backend.OCPP) and translates
/// every callback into the charger-neutral <see cref="IChargerTelemetry"/>. Every OCPP parsing/unit
/// quirk lives here — <see cref="ChargerStatusTracker"/> and <c>ChargeSessionRecorder</c> never see
/// an OCPP shape. A future Zaptec poller emits <see cref="IChargerTelemetry"/> directly and never
/// touches this adapter.
/// </summary>
public class OcppTelemetryAdapter : IChargerTelemetrySink
{
    private readonly IChargerTelemetry _telemetry;
    private readonly ILogger<OcppTelemetryAdapter> _logger;

    public OcppTelemetryAdapter(IChargerTelemetry telemetry, ILogger<OcppTelemetryAdapter> logger)
    {
        _telemetry = telemetry;
        _logger = logger;
    }

    public void OnConnected(string chargePointId) => _telemetry.OnConnected(chargePointId);

    public void OnDisconnected(string chargePointId) => _telemetry.OnDisconnected(chargePointId);

    public void OnBoot(string chargePointId, ChargerInfo info) =>
        _telemetry.OnChargerInfo(chargePointId, new ChargerDeviceInfo(info.Vendor, info.Model, info.SerialNumber, info.FirmwareVersion));

    public void OnHeartbeat(string chargePointId) => _telemetry.OnHeartbeat(chargePointId);

    public void OnConnectorStatus(string chargePointId, int connectorId, string status, string? errorCode)
    {
        var state = Enum.TryParse<ConnectorState>(status, ignoreCase: true, out var parsed) ? parsed : ConnectorState.Unknown;
        _telemetry.OnConnectorStatus(chargePointId, connectorId, state, errorCode);
    }

    public void OnTransactionStarted(string chargePointId, int connectorId, int transactionId, int meterStartWh, string? idTag, DateTimeOffset startedAt) =>
        _telemetry.OnSessionStarted(chargePointId, connectorId, transactionId, meterStartWh / 1000.0, idTag, startedAt);

    public void OnTransactionStopped(string chargePointId, int transactionId, int meterStopWh, string? reason, DateTimeOffset stoppedAt) =>
        _telemetry.OnSessionStopped(chargePointId, transactionId, meterStopWh / 1000.0, reason, stoppedAt);

    public void OnMeterValues(string chargePointId, MeterValuesRequest request)
    {
        try
        {
            foreach (var meterValue in request.MeterValue)
            {
                _telemetry.OnMeterSample(chargePointId, request.ConnectorId, BuildSample(meterValue));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error translating OCPP meter values for {ChargePointId}", chargePointId);
        }
    }

    /// <summary>
    /// Builds one <see cref="ChargerMeterSample"/> from every SampledValue in a MeterValue entry.
    /// Power.Offered, Current.Offered, Temperature, energy/current export and reactive measurands
    /// are deliberately dropped here — nothing downstream consumes them.
    /// </summary>
    private static ChargerMeterSample BuildSample(MeterValue meterValue)
    {
        double? energyKwh = null, powerKw = null, v1 = null, v2 = null, v3 = null, c1 = null, c2 = null, c3 = null, soc = null;

        foreach (var sampled in meterValue.SampledValue)
        {
            if (!double.TryParse(sampled.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var raw))
            {
                continue; // unparseable — skip
            }

            switch (sampled.Measurand ?? "Energy.Active.Import.Register")
            {
                case "Energy.Active.Import.Register":
                    // OCPP 1.6: a missing unit means Wh for energy measurands.
                    var isWh = sampled.Unit is null || sampled.Unit.Equals("wh", StringComparison.OrdinalIgnoreCase);
                    energyKwh = isWh ? raw / 1000.0 : raw;
                    break;

                case "Power.Active.Import":
                    // OCPP's default unit for Power.Active.Import is watts.
                    powerKw = sampled.Unit is not null && sampled.Unit.Equals("kW", StringComparison.OrdinalIgnoreCase)
                        ? raw
                        : raw / 1000.0;
                    break;

                case "Voltage":
                    switch (sampled.Phase)
                    {
                        case "L1": v1 = raw; break;
                        case "L2": v2 = raw; break;
                        case "L3": v3 = raw; break;
                    }
                    break;

                case "Current.Import":
                    switch (sampled.Phase)
                    {
                        case "L1": c1 = raw; break;
                        case "L2": c2 = raw; break;
                        case "L3": c3 = raw; break;
                    }
                    break;

                case "SoC":
                    soc = raw;
                    break;
            }
        }

        return new ChargerMeterSample(energyKwh, powerKw, v1, v2, v3, c1, c2, c3, soc, AsUtc(meterValue.Timestamp));
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
