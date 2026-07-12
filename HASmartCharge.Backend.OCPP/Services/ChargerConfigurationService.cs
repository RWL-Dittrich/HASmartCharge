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
    /// </summary>
    public async Task ConfigureChargerAsync(string chargePointId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[{ChargePointId}] Starting charger configuration", chargePointId);

        var config = await _configProvider.GetConfigurationAsync(chargePointId, cancellationToken);

        // First, get current configuration to see what's supported
        await GetCurrentConfigurationAsync(chargePointId, cancellationToken);

        // Sampled MeterValues interval drives per-hour cost attribution granularity
        await SetConfigurationAsync(chargePointId, "MeterValueSampleInterval",
            config.MeterValueSampleIntervalSeconds.ToString(), cancellationToken);

        await SetConfigurationAsync(chargePointId, "ClockAlignedDataInterval",
            config.ClockAlignedDataIntervalSeconds.ToString(), cancellationToken);

        // Which measurands to include in MeterValues (energy register is the cost-calc source)
        await SetConfigurationAsync(chargePointId, "MeterValuesSampledData", config.MeterValuesSampledData, cancellationToken);
        await SetConfigurationAsync(chargePointId, "MeterValuesAlignedData", config.MeterValuesSampledData, cancellationToken);

        _logger.LogInformation("[{ChargePointId}] Charger configuration completed", chargePointId);
    }

    /// <summary>
    /// Get current configuration from the charger
    /// </summary>
    private async Task GetCurrentConfigurationAsync(string chargePointId, CancellationToken cancellationToken)
    {
        try
        {
            // Request configuration for meter values related keys
            var keys = new List<string>
            {
                "MeterValueSampleInterval",
                "MeterValuesSampledData",
                "ClockAlignedDataInterval",
                "MeterValuesAlignedData",
                "StopTxnSampledData"
            };

            var request = new GetConfigurationRequest { Key = keys };
            
            var success = (await _commandSender.SendCommandAsync(
                chargePointId, 
                "GetConfiguration", 
                request, 
                cancellationToken)).Success;

            if (success)
            {
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
    }

    /// <summary>
    /// Set a configuration value on the charger
    /// </summary>
    private async Task<bool> SetConfigurationAsync(
        string chargePointId, 
        string key, 
        string value, 
        CancellationToken cancellationToken)
    {
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

