using HASmartCharge.Backend.Services.Mqtt;

namespace HASmartCharge.Backend.Tests;

public class MqttSwitchRuleTests
{
    [Theory]
    [InlineData("Unavailable", false)]
    [InlineData("unavailable", false)]
    [InlineData("Available", true)]
    [InlineData("Charging", true)]
    [InlineData(null, true)]
    public void IsOn_is_off_only_when_unavailable(string? status, bool expected)
    {
        Assert.Equal(expected, MqttSwitchRule.IsOn(status));
    }

    [Theory]
    [InlineData(true, "Available", true)]
    [InlineData(true, "Unavailable", true)]
    [InlineData(true, "Charging", false)]
    [InlineData(true, "Preparing", false)]
    [InlineData(true, "Faulted", false)]
    [InlineData(true, null, false)]
    [InlineData(false, "Available", false)]
    public void IsAvailable_requires_connected_and_a_settled_state(bool connected, string? status, bool expected)
    {
        Assert.Equal(expected, MqttSwitchRule.IsAvailable(connected, status));
    }

    [Theory]
    // Make Operative (ON) is allowed only from Unavailable.
    [InlineData(true, "Unavailable", true, true)]
    [InlineData(true, "Available", true, false)]
    // Make Inoperative (OFF) is allowed only from Available.
    [InlineData(true, "Available", false, true)]
    [InlineData(true, "Unavailable", false, false)]
    // Transitional / disconnected states never allow a toggle.
    [InlineData(true, "Charging", true, false)]
    [InlineData(false, "Available", false, false)]
    [InlineData(false, "Unavailable", true, false)]
    public void CanApply_matches_the_dashboard_rule(bool connected, string? status, bool desiredOn, bool expected)
    {
        Assert.Equal(expected, MqttSwitchRule.CanApply(connected, status, desiredOn));
    }
}
