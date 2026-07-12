using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

/// <summary>
/// OCPP 1.6 SetChargingProfile.req — used to cap the charge power the charger will
/// deliver. This is the ONE smart-charging command the app sends; charging start/stop
/// still goes through Home Assistant (see plan.md §1). Build one with
/// <see cref="ForFlatPowerLimit"/>. Reuses <see cref="ChargingProfile"/> from CommonTypes.
/// </summary>
public class SetChargingProfileRequest
{
    [JsonPropertyName("connectorId")]
    public int ConnectorId { get; set; }

    [JsonPropertyName("csChargingProfiles")]
    public ChargingProfile CsChargingProfiles { get; set; } = new();

    /// <summary>
    /// A flat current ceiling applied to every transaction on the connector: a
    /// <c>TxDefaultProfile</c> with a single, relative period limiting to <paramref name="amps"/> A
    /// per phase across <paramref name="numberPhases"/> phases. The UI works in kW; the caller
    /// converts to amps (A = W / (phases × voltage)) since OCPP charging profiles cap current, not
    /// power, on most chargers. The same <c>chargingProfileId</c>/purpose/stackLevel is reused so each
    /// call replaces the previous ceiling rather than stacking.
    /// </summary>
    public static SetChargingProfileRequest ForFlatCurrentLimit(int connectorId, double amps, int numberPhases) => new()
    {
        ConnectorId = connectorId,
        CsChargingProfiles = new ChargingProfile
        {
            ChargingProfileId = 1,
            StackLevel = 0,
            ChargingProfilePurpose = "TxDefaultProfile",
            ChargingProfileKind = "Relative",
            ChargingSchedule = new ChargingSchedule
            {
                ChargingRateUnit = "A",
                ChargingSchedulePeriod = new List<ChargingSchedulePeriod>
                {
                    new() { StartPeriod = 0, Limit = (decimal)amps, NumberPhases = numberPhases }
                }
            }
        }
    };
}

public class SetChargingProfileResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // Accepted, Rejected, NotSupported
}
