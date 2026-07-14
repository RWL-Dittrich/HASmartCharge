using System.Text.Json;
using HASmartCharge.Backend.OCPP.Models;
using Microsoft.Extensions.Logging;

namespace HASmartCharge.Backend.OCPP.Services;

/// <summary>
/// Service that automatically configures chargers for optimal data reporting
/// </summary>
public class ChargerConfigurationService
{
    private readonly ILogger<ChargerConfigurationService> _logger;
    private readonly ICommandSender _commandSender;
    private readonly IOcppChargerConfigurationProvider _configProvider;

    public ChargerConfigurationService(
        ILogger<ChargerConfigurationService> logger,
        ICommandSender commandSender,
        IOcppChargerConfigurationProvider configProvider)
    {
        _logger = logger;
        _commandSender = commandSender;
        _configProvider = configProvider;
    }

    /// <summary>
    /// Configure a charger for optimal MeterValues reporting after connection.
    /// Values come from ChargerSettings via <see cref="IOcppChargerConfigurationProvider"/>.
    /// Keys already at the desired value are skipped to keep the config push idempotent —
    /// some chargers reset or drop the connection when configuration is (re)written.
    /// </summary>
    public async Task ConfigureChargerAsync(string chargePointId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[{ChargePointId}] Starting charger configuration", chargePointId);

        var config = await _configProvider.GetConfigurationAsync(chargePointId, cancellationToken);

        // First, get current configuration so unchanged keys can be skipped
        var current = await GetCurrentConfigurationAsync(chargePointId, cancellationToken);

        // Heartbeat interval keeps traffic flowing on an otherwise idle connection so
        // NAT/proxy/conntrack entries stay warm. The BootNotification response also
        // carries it, but a charger that reconnects without re-booting never sees that
        // response — push the key explicitly. OCPP 1.5-era firmwares spell it with a
        // capital B; use that spelling when it's the only one the charger reports.
        var heartbeatKey = current.ContainsKey("HeartBeatInterval") && !current.ContainsKey("HeartbeatInterval")
            ? "HeartBeatInterval"
            : "HeartbeatInterval";
        await SetConfigurationAsync(chargePointId, heartbeatKey,
            config.HeartbeatIntervalSeconds.ToString(), current, cancellationToken);

        // Sampled MeterValues interval drives per-hour cost attribution granularity
        await SetConfigurationAsync(chargePointId, "MeterValueSampleInterval",
            config.MeterValueSampleIntervalSeconds.ToString(), current, cancellationToken);

        await SetConfigurationAsync(chargePointId, "ClockAlignedDataInterval",
            config.ClockAlignedDataIntervalSeconds.ToString(), current, cancellationToken);

        // Which measurands to include in MeterValues (energy register is the cost-calc source)
        await SetConfigurationAsync(chargePointId, "MeterValuesSampledData", config.MeterValuesSampledData, current, cancellationToken);
        await SetConfigurationAsync(chargePointId, "MeterValuesAlignedData", config.MeterValuesSampledData, current, cancellationToken);

        _logger.LogInformation("[{ChargePointId}] Charger configuration completed", chargePointId);
    }

    /// <summary>
    /// Get current configuration from the charger as a key/value map.
    /// Returns an empty map when the request fails, so all keys get written.
    /// </summary>
    private async Task<Dictionary<string, string?>> GetCurrentConfigurationAsync(string chargePointId, CancellationToken cancellationToken)
    {
        var currentValues = new Dictionary<string, string?>(StringComparer.Ordinal);

        try
        {
            // Request configuration for meter values related keys
            var keys = new List<string>
            {
                "HeartbeatInterval",
                "HeartBeatInterval",
                "MeterValueSampleInterval",
                "MeterValuesSampledData",
                "ClockAlignedDataInterval",
                "MeterValuesAlignedData",
                "StopTxnSampledData"
            };

            var request = new GetConfigurationRequest { Key = keys };

            var result = await _commandSender.SendCommandAsync(
                chargePointId,
                "GetConfiguration",
                request,
                cancellationToken);

            if (result.Success && result.RawPayload is { } payload)
            {
                var response = JsonSerializer.Deserialize<GetConfigurationResponse>(payload.GetRawText());
                foreach (var entry in response?.ConfigurationKey ?? [])
                {
                    currentValues[entry.Key] = entry.Value;
                }

                _logger.LogInformation("[{ChargePointId}] Successfully retrieved configuration", chargePointId);
            }
            else
            {
                _logger.LogWarning("[{ChargePointId}] Failed to retrieve configuration", chargePointId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{ChargePointId}] Error getting configuration", chargePointId);
        }

        return currentValues;
    }

    /// <summary>
    /// Set a configuration value on the charger, skipping keys already at the desired value
    /// </summary>
    private async Task<bool> SetConfigurationAsync(
        string chargePointId,
        string key,
        string value,
        Dictionary<string, string?> currentValues,
        CancellationToken cancellationToken)
    {
        if (currentValues.TryGetValue(key, out var existing) && existing == value)
        {
            _logger.LogInformation("[{ChargePointId}] Configuration {Key} already {Value}, skipping",
                chargePointId, key, value);
            return true;
        }

        try
        {
            var request = new ChangeConfigurationRequest
            {
                Key = key,
                Value = value
            };

            var success = (await _commandSender.SendCommandAsync(
                chargePointId,
                "ChangeConfiguration",
                request,
                cancellationToken)).Success;

            if (success)
            {
                _logger.LogInformation("[{ChargePointId}] Set configuration {Key} = {Value}",
                    chargePointId, key, value);
                return true;
            }
            else
            {
                _logger.LogWarning("[{ChargePointId}] Failed to set configuration {Key} = {Value}",
                    chargePointId, key, value);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{ChargePointId}] Error setting configuration {Key}", chargePointId, key);
            return false;
        }
    }

}
