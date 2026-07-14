namespace HASmartCharge.Backend.Services.Mqtt;

/// <summary>
/// Builds the MQTT topic tree from the configured base topic and HA discovery prefix.
///
/// The discovery node id is the fixed literal <c>hasmartcharge</c> — deliberately NOT the
/// ChargePointId, which may be empty, contain topic-illegal characters, or be renamed by the
/// user (renaming would orphan retained configs and duplicate the HA device). ChargePointId is
/// surfaced in the device <c>model</c> info instead.
/// </summary>
public sealed class MqttTopics
{
    public const string NodeId = "hasmartcharge";

    private readonly string _discoveryPrefix;

    public MqttTopics(string baseTopic, string discoveryPrefix)
    {
        var b = baseTopic.Trim().Trim('/');
        _discoveryPrefix = discoveryPrefix.Trim().Trim('/');

        Status = $"{b}/status";

        PowerKw = $"{b}/charger/power_kw";
        CarSoc = $"{b}/charger/car_soc";
        Connected = $"{b}/charger/connected";
        ConnectorStatus = $"{b}/charger/connector_status";
        SessionEnergyKwh = $"{b}/charger/session_energy_kwh";
        SessionCost = $"{b}/charger/session_cost";
        LastHeartbeat = $"{b}/charger/last_heartbeat";

        PlanDeadline = $"{b}/plan/deadline";
        PlanTargetSoc = $"{b}/plan/target_soc";
        PlanRequiredKwh = $"{b}/plan/required_kwh";
        PlanEstimatedCost = $"{b}/plan/estimated_cost";

        SwitchState = $"{b}/switch/operative/state";
        SwitchAvailable = $"{b}/switch/operative/available";
        SwitchSet = $"{b}/switch/operative/set";

        HaBirth = $"{_discoveryPrefix}/status";
    }

    /// <summary>App availability / LWT topic ("online"/"offline"), retained.</summary>
    public string Status { get; }

    public string PowerKw { get; }
    public string CarSoc { get; }
    public string Connected { get; }
    public string ConnectorStatus { get; }
    public string SessionEnergyKwh { get; }
    public string SessionCost { get; }
    public string LastHeartbeat { get; }

    public string PlanDeadline { get; }
    public string PlanTargetSoc { get; }
    public string PlanRequiredKwh { get; }
    public string PlanEstimatedCost { get; }

    public string SwitchState { get; }
    public string SwitchAvailable { get; }
    public string SwitchSet { get; }

    /// <summary>HA birth/last-will topic we subscribe to; "online" means HA (re)started.</summary>
    public string HaBirth { get; }

    /// <summary>Retained per-component discovery config topic: {prefix}/{component}/hasmartcharge/{objectId}/config.</summary>
    public string DiscoveryConfig(string component, string objectId) =>
        $"{_discoveryPrefix}/{component}/{NodeId}/{objectId}/config";
}
