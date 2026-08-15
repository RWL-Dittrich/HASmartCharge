using HASmartCharge.Core.Charging;

namespace HASmartCharge.Core.Tests;

public class ChargePowerUnitsTests
{
    [Theory]
    [InlineData("A", 7.4, 10.7, "10.7")]
    [InlineData("a", 7.4, 16.0, "16")]
    [InlineData("mA", 7.4, 10.7, "10700")]
    [InlineData("W", 7.4, 10.7, "7400")]
    [InlineData("kW", 7.4, 10.7, "7.4")]
    [InlineData("kW", 11, 16, "11")]
    public void FormatsPerUnit(string unit, double kw, double amps, string expected)
    {
        Assert.True(ChargePowerUnits.TryFormat(unit, kw, amps, out var value));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void RoundsDownNeverUp()
    {
        Assert.True(ChargePowerUnits.TryFormat("A", 4.1, 5.99, out var value));
        Assert.Equal("5.9", value);
    }

    [Fact]
    public void RejectsUnknownUnit()
    {
        Assert.False(ChargePowerUnits.TryFormat("horsepower", 7.4, 10.7, out var value));
        Assert.Null(value);
    }
}
