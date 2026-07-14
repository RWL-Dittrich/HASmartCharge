using System.Text.Json;
using HASmartCharge.Backend.DB.Models;
using HASmartCharge.Backend.Services.Mqtt;

namespace HASmartCharge.Backend.Tests;

public class HaDiscoveryConfigBuilderTests
{
    private static MqttSettings Settings(string baseTopic = "hasmartcharge", string prefix = "homeassistant") =>
        new() { BaseTopic = baseTopic, DiscoveryPrefix = prefix };

    private static IReadOnlyList<DiscoveryConfig> Build(
        MqttSettings? settings = null, string currency = "EUR", string swVersion = "1.0.0", string chargePointId = "CP-1") =>
        HaDiscoveryConfigBuilder.Build(settings ?? Settings(), currency, swVersion, chargePointId);

    private static JsonElement PayloadFor(IReadOnlyList<DiscoveryConfig> configs, string component, string objectId)
    {
        var topic = $"homeassistant/{component}/hasmartcharge/{objectId}/config";
        var config = configs.Single(c => c.Topic == topic);
        return JsonDocument.Parse(config.Payload).RootElement;
    }

    [Fact]
    public void Build_produces_all_twelve_entities()
    {
        Assert.Equal(12, Build().Count);
    }

    [Fact]
    public void Discovery_topics_follow_prefix_component_node_object_config()
    {
        var configs = Build(Settings(prefix: "ha_custom"));
        Assert.Contains(configs, c => c.Topic == "ha_custom/sensor/hasmartcharge/power/config");
        Assert.Contains(configs, c => c.Topic == "ha_custom/binary_sensor/hasmartcharge/connected/config");
        Assert.Contains(configs, c => c.Topic == "ha_custom/switch/hasmartcharge/operative/config");
    }

    [Fact]
    public void State_topics_use_the_configured_base_topic()
    {
        var power = PayloadFor(Build(Settings(baseTopic: "myhome/ev")), "sensor", "power");
        Assert.Equal("myhome/ev/charger/power_kw", power.GetProperty("state_topic").GetString());
    }

    [Fact]
    public void Power_sensor_carries_power_class_measurement_and_kw()
    {
        var power = PayloadFor(Build(), "sensor", "power");
        Assert.Equal("power", power.GetProperty("device_class").GetString());
        Assert.Equal("measurement", power.GetProperty("state_class").GetString());
        Assert.Equal("kW", power.GetProperty("unit_of_measurement").GetString());
        Assert.Equal("hasmartcharge_power", power.GetProperty("unique_id").GetString());
    }

    [Fact]
    public void Monetary_sensors_use_the_configured_currency()
    {
        var configs = Build(currency: "USD");
        Assert.Equal("USD", PayloadFor(configs, "sensor", "session_cost").GetProperty("unit_of_measurement").GetString());
        Assert.Equal("USD", PayloadFor(configs, "sensor", "plan_estimated_cost").GetProperty("unit_of_measurement").GetString());
    }

    [Fact]
    public void Session_energy_omits_state_class_to_protect_long_term_stats()
    {
        var energy = PayloadFor(Build(), "sensor", "session_energy");
        Assert.Equal("energy", energy.GetProperty("device_class").GetString());
        Assert.False(energy.TryGetProperty("state_class", out _));
    }

    [Fact]
    public void Timestamp_and_enum_entities_carry_no_unit_or_state_class()
    {
        foreach (var (component, objectId) in new[] { ("sensor", "last_heartbeat"), ("sensor", "plan_deadline") })
        {
            var el = PayloadFor(Build(), component, objectId);
            Assert.Equal("timestamp", el.GetProperty("device_class").GetString());
            Assert.False(el.TryGetProperty("unit_of_measurement", out _));
            Assert.False(el.TryGetProperty("state_class", out _));
        }

        var connectorStatus = PayloadFor(Build(), "sensor", "connector_status");
        Assert.Equal("enum", connectorStatus.GetProperty("device_class").GetString());
        Assert.False(connectorStatus.TryGetProperty("unit_of_measurement", out _));
        Assert.False(connectorStatus.TryGetProperty("state_class", out _));
    }

    [Fact]
    public void Connector_status_enum_lists_the_nine_ocpp_states()
    {
        var options = PayloadFor(Build(), "sensor", "connector_status")
            .GetProperty("options").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Equal(9, options.Length);
        Assert.Contains("Available", options);
        Assert.Contains("Faulted", options);
        Assert.Contains("SuspendedEVSE", options);
    }

    [Fact]
    public void Switch_has_command_topic_dual_availability_and_mode_all()
    {
        var sw = PayloadFor(Build(), "switch", "operative");
        Assert.Equal("hasmartcharge/switch/operative/set", sw.GetProperty("command_topic").GetString());
        Assert.Equal("all", sw.GetProperty("availability_mode").GetString());
        Assert.False(sw.GetProperty("optimistic").GetBoolean());

        var availabilityTopics = sw.GetProperty("availability").EnumerateArray()
            .Select(a => a.GetProperty("topic").GetString()).ToArray();
        Assert.Contains("hasmartcharge/status", availabilityTopics);
        Assert.Contains("hasmartcharge/switch/operative/available", availabilityTopics);
    }

    [Fact]
    public void Connected_binary_sensor_depends_only_on_app_availability()
    {
        var connected = PayloadFor(Build(), "binary_sensor", "connected");
        Assert.Equal("connectivity", connected.GetProperty("device_class").GetString());
        var availabilityTopics = connected.GetProperty("availability").EnumerateArray()
            .Select(a => a.GetProperty("topic").GetString()).ToArray();
        Assert.Equal(new[] { "hasmartcharge/status" }, availabilityTopics);
    }

    [Fact]
    public void Every_entity_shares_one_device_and_carries_origin()
    {
        foreach (var config in Build(swVersion: "2.3.4", chargePointId: "CP-42"))
        {
            var root = JsonDocument.Parse(config.Payload).RootElement;
            var device = root.GetProperty("device");
            Assert.Equal(new[] { "hasmartcharge" }, device.GetProperty("identifiers").EnumerateArray().Select(e => e.GetString()).ToArray());
            Assert.Equal("HASmartCharge", device.GetProperty("name").GetString());
            Assert.Equal("2.3.4", device.GetProperty("sw_version").GetString());
            Assert.Contains("CP-42", device.GetProperty("model").GetString());
            Assert.Equal("HASmartCharge", root.GetProperty("origin").GetProperty("name").GetString());
        }
    }

    [Fact]
    public void Model_falls_back_when_charge_point_id_is_blank()
    {
        var device = PayloadFor(Build(chargePointId: ""), "sensor", "power").GetProperty("device");
        Assert.Equal("OCPP smart-charging bridge", device.GetProperty("model").GetString());
    }
}
