using HASmartCharge.Backend.OCPP.Models;
using Microsoft.Extensions.Logging;

namespace HASmartCharge.Backend.OCPP.Services;

/// <summary>
/// Service that automatically configures chargers for optimal data reporting
/// </summary>
public class ChargerConfigurationService
{
    private readonly ILogger<ChargerConfigurationService> _logger;
    private readonly ChargerConnectionManager _connectionManager;

    public ChargerConfigurationService(
        ILogger<ChargerConfigurationService> logger,
        ChargerConnectionManager connectionManager)
    {
        _logger = logger;
        _connectionManager = connectionManager;
    }

    /// <summary>
    /// Configure a charger for optimal MeterValues reporting after connection
    /// </summary>
    public async Task ConfigureChargerAsync(string chargePointId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[{ChargePointId}] Starting charger configuration", chargePointId);

        // First, get current configuration to see what's supported
        await GetCurrentConfigurationAsync(chargePointId, cancellationToken);

        // Configure MeterValues reporting interval (how often to send data)
        // Standard interval is 60 seconds, we want more frequent updates
        await SetConfigurationAsync(chargePointId, "MeterValueSampleInterval", "10", cancellationToken);
        
        // Configure clock-aligned data interval (synced reporting)
        await SetConfigurationAsync(chargePointId, "ClockAlignedDataInterval", "10", cancellationToken);

        // Configure which measurands to include in MeterValues
        // This is the key to getting power data!
        string measurands = "Power.Active.Import,Energy.Active.Import.Register,Current.Import,Voltage,Current.Offered,Power.Offered,SoC,Voltage.L1,Voltage.L2,Voltage.L3";
        await SetConfigurationAsync(chargePointId, "MeterValuesSampledData", measurands, cancellationToken);

        // Also configure sampled data for transactions (during charging)
        // await SetConfigurationAsync(chargePointId, "StopTxnSampledData", "false", cancellationToken);

        // Configure the charger to include all phases for voltage and current
        await SetConfigurationAsync(chargePointId, "MeterValuesAlignedData", measurands, cancellationToken);

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
            List<string> keys = new List<string>
            {
                "MeterValueSampleInterval",
                "MeterValuesSampledData",
                "ClockAlignedDataInterval",
                "MeterValuesAlignedData",
                "StopTxnSampledData"
            };

            GetConfigurationRequest request = new GetConfigurationRequest { Key = keys };
            
            bool success = await _connectionManager.SendCommandAsync(
                chargePointId, 
                "GetConfiguration", 
                request, 
                cancellationToken);

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
            ChangeConfigurationRequest request = new ChangeConfigurationRequest
            {
                Key = key,
                Value = value
            };

            bool success = await _connectionManager.SendCommandAsync(
                chargePointId, 
                "ChangeConfiguration", 
                request, 
                cancellationToken);

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

    /// <summary>
    /// Configure minimal settings for testing (less aggressive)
    /// </summary>
    public async Task ConfigureChargerMinimalAsync(string chargePointId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[{ChargePointId}] Starting minimal charger configuration", chargePointId);

        // Just set the most important ones
        await SetConfigurationAsync(chargePointId, "MeterValueSampleInterval", "10", cancellationToken);
        
        string measurands = "Power.Active.Import,Current.Import,Voltage";
        await SetConfigurationAsync(chargePointId, "MeterValuesSampledData", measurands, cancellationToken);

        _logger.LogInformation("[{ChargePointId}] Minimal charger configuration completed", chargePointId);
    }
}

