using System.Text.Json;
using HASmartCharge.Backend.DB.Models;

namespace HASmartCharge.Backend.Services.Mqtt;

/// <summary>A single HA MQTT discovery config: the retained topic and its JSON payload.</summary>
public record DiscoveryConfig(string Topic, string Payload);

/// <summary>
/// Pure builder for the 12 Home Assistant MQTT discovery configs that make up the HASmartCharge
/// device. Per-component (classic) discovery: one retained config per entity, all sharing the same
/// <c>device.identifiers</c> so HA groups them under one device.
///
/// No side effects — settings + currency + version in, (topic, json) pairs out — so it is fully
/// unit-testable. HA rejects a config that carries a unit/state_class on a timestamp/enum class, so
/// those keys are simply never added for those entities.
/// </summary>
public static class HaDiscoveryConfigBuilder
{
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = false };

    // The 9 OCPP 1.6 ChargePointStatus values, exposed as the enum sensor's allowed options.
    private static readonly string[] _connectorStatusOptions =
    [
        "Available", "Preparing", "Charging", "SuspendedEVSE", "SuspendedEV",
        "Finishing", "Reserved", "Unavailable", "Faulted"
    ];

    public static IReadOnlyList<DiscoveryConfig> Build(MqttSettings settings, string currency, string swVersion, string chargePointId)
    {
        var topics = new MqttTopics(settings.BaseTopic, settings.DiscoveryPrefix);

        var model = string.IsNullOrWhiteSpace(chargePointId)
            ? "OCPP smart-charging bridge"
            : $"OCPP smart-charging bridge ({chargePointId})";

        Dictionary<string, object?> Device() => new()
        {
            ["identifiers"] = new[] { MqttTopics.NodeId },
            ["name"] = "HASmartCharge",
            ["manufacturer"] = "HASmartCharge",
            ["model"] = model,
            ["sw_version"] = swVersion,
        };

        Dictionary<string, object?> Origin() => new()
        {
            ["name"] = "HASmartCharge",
            ["sw"] = swVersion,
        };

        object[] AppAvailability() =>
        [
            new Dictionary<string, object?> { ["topic"] = topics.Status }
        ];

        Dictionary<string, object?> Base(string objectId, string name, string stateTopic) => new()
        {
            ["name"] = name,
            ["unique_id"] = $"{MqttTopics.NodeId}_{objectId}",
            ["state_topic"] = stateTopic,
            ["availability"] = AppAvailability(),
            ["device"] = Device(),
            ["origin"] = Origin(),
        };

        var configs = new List<DiscoveryConfig>();

        void Add(string component, string objectId, Dictionary<string, object?> config) =>
            configs.Add(new DiscoveryConfig(topics.DiscoveryConfig(component, objectId), JsonSerializer.Serialize(config, _json)));

        // --- Sensors ---

        var power = Base("power", "Current power", topics.PowerKw);
        power["device_class"] = "power";
        power["state_class"] = "measurement";
        power["unit_of_measurement"] = "kW";
        power["suggested_display_precision"] = 2;
        Add("sensor", "power", power);

        var carSoc = Base("car_soc", "Current charge", topics.CarSoc);
        carSoc["device_class"] = "battery";
        carSoc["state_class"] = "measurement";
        carSoc["unit_of_measurement"] = "%";
        carSoc["suggested_display_precision"] = 0;
        Add("sensor", "car_soc", carSoc);

        var connectorStatus = Base("connector_status", "Connector status", topics.ConnectorStatus);
        connectorStatus["device_class"] = "enum";
        connectorStatus["options"] = _connectorStatusOptions;
        Add("sensor", "connector_status", connectorStatus);

        // Session energy resets per session, so it must NOT be total_increasing (that would corrupt
        // HA long-term statistics), and energy device_class disallows state_class: measurement — so
        // state_class is omitted entirely. A separate lifetime-register total_increasing sensor is a
        // possible future addition if long-term energy stats are wanted.
        var sessionEnergy = Base("session_energy", "Session energy", topics.SessionEnergyKwh);
        sessionEnergy["device_class"] = "energy";
        sessionEnergy["unit_of_measurement"] = "kWh";
        sessionEnergy["suggested_display_precision"] = 3;
        Add("sensor", "session_energy", sessionEnergy);

        var sessionCost = Base("session_cost", "Session cost", topics.SessionCost);
        sessionCost["device_class"] = "monetary";
        sessionCost["unit_of_measurement"] = currency;
        sessionCost["suggested_display_precision"] = 2;
        Add("sensor", "session_cost", sessionCost);

        var lastHeartbeat = Base("last_heartbeat", "Last heartbeat", topics.LastHeartbeat);
        lastHeartbeat["device_class"] = "timestamp";
        lastHeartbeat["entity_category"] = "diagnostic";
        Add("sensor", "last_heartbeat", lastHeartbeat);

        var planDeadline = Base("plan_deadline", "Plan deadline", topics.PlanDeadline);
        planDeadline["device_class"] = "timestamp";
        Add("sensor", "plan_deadline", planDeadline);

        // Target SoC is a goal, not a live battery level, so no battery device_class (which HA would
        // treat as a measurement).
        var planTargetSoc = Base("plan_target_soc", "Plan target charge", topics.PlanTargetSoc);
        planTargetSoc["unit_of_measurement"] = "%";
        planTargetSoc["icon"] = "mdi:battery-charging-90";
        Add("sensor", "plan_target_soc", planTargetSoc);

        var planRequiredKwh = Base("plan_required_kwh", "Plan required energy", topics.PlanRequiredKwh);
        planRequiredKwh["device_class"] = "energy";
        planRequiredKwh["unit_of_measurement"] = "kWh";
        planRequiredKwh["suggested_display_precision"] = 2;
        Add("sensor", "plan_required_kwh", planRequiredKwh);

        var planEstimatedCost = Base("plan_estimated_cost", "Plan estimated cost", topics.PlanEstimatedCost);
        planEstimatedCost["device_class"] = "monetary";
        planEstimatedCost["unit_of_measurement"] = currency;
        planEstimatedCost["suggested_display_precision"] = 2;
        Add("sensor", "plan_estimated_cost", planEstimatedCost);

        // --- Binary sensor ---

        // Depends ONLY on the app LWT (the default availability), so it shows OFF when the charger
        // drops rather than going "unavailable"; it goes unavailable only when the whole app is down.
        var connected = Base("connected", "Charger connected", topics.Connected);
        connected["device_class"] = "connectivity";
        connected["payload_on"] = "ON";
        connected["payload_off"] = "OFF";
        connected["entity_category"] = "diagnostic";
        Add("binary_sensor", "connected", connected);

        // --- Switch (Operative/Inoperative) ---

        // Dual availability with availability_mode "all": the entity is available only when BOTH the
        // app LWT is online AND the connector is in a togglable state (switch/operative/available),
        // so HA greys the toggle out exactly like the dashboard does.
        var operative = Base("operative", "Operative", topics.SwitchState);
        operative["command_topic"] = topics.SwitchSet;
        operative["payload_on"] = "ON";
        operative["payload_off"] = "OFF";
        operative["state_on"] = "ON";
        operative["state_off"] = "OFF";
        operative["optimistic"] = false;
        operative["icon"] = "mdi:ev-station";
        operative["availability"] = new object[]
        {
            new Dictionary<string, object?> { ["topic"] = topics.Status },
            new Dictionary<string, object?> { ["topic"] = topics.SwitchAvailable },
        };
        operative["availability_mode"] = "all";
        Add("switch", "operative", operative);

        return configs;
    }
}
