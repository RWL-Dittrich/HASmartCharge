using HASmartCharge.Backend.OCPP.Models;
using HASmartCharge.Backend.Services.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;

namespace HASmartCharge.Backend.Tests;

public class OcppTelemetryAdapterTests
{
    private static (OcppTelemetryAdapter adapter, ChargerStatusTracker tracker) Build()
    {
        var tracker = new ChargerStatusTracker(NullLogger<ChargerStatusTracker>.Instance);
        return (new OcppTelemetryAdapter(tracker, NullLogger<OcppTelemetryAdapter>.Instance), tracker);
    }

    private static MeterValuesRequest Req(int connectorId, DateTime ts, params SampledValue[] values) =>
        new() { ConnectorId = connectorId, MeterValue = [new MeterValue { Timestamp = ts, SampledValue = values.ToList() }] };

    [Fact]
    public void EnergyMissingUnit_IsWhToKwh()
    {
        var (a, t) = Build();
        a.OnMeterValues("cp", Req(1, DateTime.UtcNow, new SampledValue { Value = "12345" }));
        Assert.Equal(12.345, t.GetConnectorMeasurands("cp", 1)!.EnergyRegisterKwh!.Value, 6);
    }

    [Fact]
    public void EnergyKwhUnit_IsKept()
    {
        var (a, t) = Build();
        a.OnMeterValues("cp", Req(1, DateTime.UtcNow, new SampledValue { Value = "12.345", Unit = "kWh", Measurand = "Energy.Active.Import.Register" }));
        Assert.Equal(12.345, t.GetConnectorMeasurands("cp", 1)!.EnergyRegisterKwh!.Value, 6);
    }

    [Fact]
    public void PowerNoUnit_IsWattsToKw_AndKwUnitKept()
    {
        var (a, t) = Build();
        a.OnMeterValues("cp", Req(1, DateTime.UtcNow, new SampledValue { Value = "7400", Measurand = "Power.Active.Import" }));
        Assert.Equal(7.4, t.GetConnectorMeasurands("cp", 1)!.PowerKw!.Value, 6);

        a.OnMeterValues("cp", Req(1, DateTime.UtcNow, new SampledValue { Value = "7.4", Unit = "kW", Measurand = "Power.Active.Import" }));
        Assert.Equal(7.4, t.GetConnectorMeasurands("cp", 1)!.PowerKw!.Value, 6);
    }

    [Fact]
    public void PhasesAndSoc_AreMapped_AndPartialUpdateKeepsPrevious()
    {
        var (a, t) = Build();
        a.OnMeterValues("cp", Req(1, DateTime.UtcNow,
            new SampledValue { Value = "230.1", Measurand = "Voltage", Phase = "L1" },
            new SampledValue { Value = "231.2", Measurand = "Voltage", Phase = "L2" },
            new SampledValue { Value = "232.3", Measurand = "Voltage", Phase = "L3" },
            new SampledValue { Value = "16.1", Measurand = "Current.Import", Phase = "L1" },
            new SampledValue { Value = "16.2", Measurand = "Current.Import", Phase = "L2" },
            new SampledValue { Value = "16.3", Measurand = "Current.Import", Phase = "L3" },
            new SampledValue { Value = "64", Measurand = "SoC" },
            new SampledValue { Value = "1000", Measurand = "Energy.Active.Import.Register" }));

        var m = t.GetConnectorMeasurands("cp", 1)!;
        Assert.Equal(230.1, m.VoltageL1!.Value, 3);
        Assert.Equal(232.3, m.VoltageL3!.Value, 3);
        Assert.Equal(16.2, m.CurrentL2!.Value, 3);
        Assert.Equal(64, m.SocPercent!.Value, 3);
        Assert.Equal(1.0, m.EnergyRegisterKwh!.Value, 6);

        // energy-only sample must not wipe voltage/soc
        a.OnMeterValues("cp", Req(1, DateTime.UtcNow, new SampledValue { Value = "2000" }));
        m = t.GetConnectorMeasurands("cp", 1)!;
        Assert.Equal(2.0, m.EnergyRegisterKwh!.Value, 6);
        Assert.Equal(230.1, m.VoltageL1!.Value, 3);
        Assert.Equal(64, m.SocPercent!.Value, 3);
    }

    [Fact]
    public void UnparseableValue_IsSkipped_NotThrown()
    {
        var (a, t) = Build();
        a.OnMeterValues("cp", Req(1, DateTime.UtcNow, new SampledValue { Value = "nope" }));
        Assert.Null(t.GetConnectorMeasurands("cp", 1)!.EnergyRegisterKwh);
    }

    [Theory]
    [InlineData("Available", "Available")]
    [InlineData("SuspendedEV", "SuspendedEV")]
    [InlineData("SuspendedEVSE", "SuspendedEVSE")]
    [InlineData("Charging", "Charging")]
    [InlineData("Preparing", "Preparing")]
    [InlineData("Finishing", "Finishing")]
    [InlineData("Reserved", "Reserved")]
    [InlineData("Unavailable", "Unavailable")]
    [InlineData("Faulted", "Faulted")]
    [InlineData("Occupied", "Unknown")]
    public void StatusStrings_RoundTripThroughEnum(string ocpp, string stored)
    {
        var (a, t) = Build();
        a.OnConnectorStatus("cp", 1, ocpp, null);
        Assert.Equal(stored, t.GetConnectorStatus("cp", 1)!.Status);
    }

    [Fact]
    public void TransactionStart_ConvertsWhToKwh_AndTerminalStateClears()
    {
        var (a, t) = Build();
        var at = DateTimeOffset.UtcNow;
        a.OnTransactionStarted("cp", 1, 42, 12345, "tag", at);
        var c = t.GetConnectorStatus("cp", 1)!;
        Assert.Equal(42, c.ActiveTransactionId);
        Assert.Equal(12.345, c.MeterStartKwh!.Value, 6);
        Assert.Equal("tag", c.IdTag);

        a.OnConnectorStatus("cp", 1, "Charging", null);
        Assert.Equal(42, t.GetConnectorStatus("cp", 1)!.ActiveTransactionId);

        a.OnConnectorStatus("cp", 1, "Finishing", null);
        Assert.Null(t.GetConnectorStatus("cp", 1)!.ActiveTransactionId);
    }

    [Fact]
    public void TransactionStopped_ClearsConnector()
    {
        var (a, t) = Build();
        a.OnTransactionStarted("cp", 1, 7, 1000, null, DateTimeOffset.UtcNow);
        a.OnTransactionStopped("cp", 7, 5000, "Local", DateTimeOffset.UtcNow);
        Assert.Null(t.GetConnectorStatus("cp", 1)!.ActiveTransactionId);
    }

    [Fact]
    public void Boot_MapsDeviceInfo()
    {
        var (a, t) = Build();
        a.OnBoot("cp", new ChargerInfo { Vendor = "V", Model = "M", SerialNumber = "S", FirmwareVersion = "F" });
        Assert.Equal(new ChargerDeviceInfo("V", "M", "S", "F"), t.GetChargerStatus("cp")!.Info);
    }

    [Fact]
    public void UnspecifiedKindTimestamp_BecomesUtc()
    {
        DateTime? seen = null;
        var sink = new CapturingSink(s => seen = s.TimestampUtc);
        var a = new OcppTelemetryAdapter(sink, NullLogger<OcppTelemetryAdapter>.Instance);
        a.OnMeterValues("cp", Req(1, new DateTime(2026, 8, 21, 10, 0, 0, DateTimeKind.Unspecified), new SampledValue { Value = "1" }));
        Assert.Equal(DateTimeKind.Utc, seen!.Value.Kind);
        Assert.Equal(new DateTime(2026, 8, 21, 10, 0, 0, DateTimeKind.Utc), seen.Value);
    }

    [Fact]
    public void MultipleMeterValueEntries_ProduceOneSampleEach()
    {
        var count = 0;
        var sink = new CapturingSink(_ => count++);
        var a = new OcppTelemetryAdapter(sink, NullLogger<OcppTelemetryAdapter>.Instance);
        a.OnMeterValues("cp", new MeterValuesRequest
        {
            ConnectorId = 1,
            MeterValue =
            [
                new MeterValue { Timestamp = DateTime.UtcNow, SampledValue = [new SampledValue { Value = "1000" }] },
                new MeterValue { Timestamp = DateTime.UtcNow, SampledValue = [new SampledValue { Value = "2000" }] }
            ]
        });
        Assert.Equal(2, count);
    }

    private sealed class CapturingSink(Action<ChargerMeterSample> onSample) : IChargerTelemetry
    {
        public void OnConnected(string chargerId) { }
        public void OnDisconnected(string chargerId) { }
        public void OnChargerInfo(string chargerId, ChargerDeviceInfo info) { }
        public void OnHeartbeat(string chargerId) { }
        public void OnConnectorStatus(string chargerId, int connectorId, ConnectorState state, string? errorCode) { }
        public void OnSessionStarted(string chargerId, int connectorId, int sessionId, double meterStartKwh, string? tag, DateTimeOffset startedAt) { }
        public void OnSessionStopped(string chargerId, int sessionId, double meterStopKwh, string? reason, DateTimeOffset stoppedAt) { }
        public void OnMeterSample(string chargerId, int connectorId, ChargerMeterSample sample) => onSample(sample);
    }
}
