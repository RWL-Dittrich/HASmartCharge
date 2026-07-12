namespace HASmartCharge.Backend.DB.Models;

/// <summary>
/// The one car we charge. Single row (Id = 1).
/// </summary>
public class CarSettings
{
    public int Id { get; set; }

    public string Name { get; set; } = "My EV";

    public double BatteryCapacityKwh { get; set; } = 75;

    public int TargetSocPercent { get; set; } = 100;

    /// <summary>Grid → battery efficiency used for energy math (0..1).</summary>
    public double ChargeEfficiency { get; set; } = 0.90;

    /// <summary>HA entity reporting battery state of charge, e.g. sensor.car_battery_level.</summary>
    public string HaSocEntityId { get; set; } = string.Empty;

    // Service call that starts charging (e.g. button.press / switch.turn_on / script.turn_on).
    public string HaStartDomain { get; set; } = string.Empty;
    public string HaStartService { get; set; } = string.Empty;
    /// <summary>Optional JSON payload for the start call (entity_id etc.).</summary>
    public string? HaStartDataJson { get; set; }

    // Service call that stops charging.
    public string HaStopDomain { get; set; } = string.Empty;
    public string HaStopService { get; set; } = string.Empty;
    public string? HaStopDataJson { get; set; }

    /// <summary>Optional binary_sensor: is the car plugged in.</summary>
    public string? HaPluggedInEntityId { get; set; }

    /// <summary>Optional entity to cross-check that the car is actually charging.</summary>
    public string? HaChargingStateEntityId { get; set; }

    /// <summary>Optional entity to push the target SoC to the car.</summary>
    public string? HaTargetSocEntityId { get; set; }
}
