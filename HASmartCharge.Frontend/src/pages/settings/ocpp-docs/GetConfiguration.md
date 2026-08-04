# GetConfiguration

Reads configuration keys from the charger. Omit `key` entirely to dump every key the charger
exposes — the fastest way to learn what a given model supports.

## Request

| Field | Type | Required | Values / notes |
| --- | --- | --- | --- |
| `key` | array of string (50) | no | Keys to read. Omit or send `[]` to return all keys. |

## Response

| Field | Type | Notes |
| --- | --- | --- |
| `configurationKey` | array of KeyValue | One entry per known key. |
| `unknownKey` | array of string | Requested keys the charger does not know. |

**KeyValue**

| Field | Type | Notes |
| --- | --- | --- |
| `key` | string (50) | Key name. |
| `readonly` | boolean | `true` means ChangeConfiguration will be rejected. |
| `value` | string (500) | Absent when the key has no value set. |

## Notes

- Keys this app cares about: `HeartbeatInterval`, `MeterValueSampleInterval`,
  `MeterValuesSampledData`, `ClockAlignedDataInterval`, `ConnectionTimeOut`. They are pushed
  on every connect (see the charger settings tab).
- Check `ChargeProfileMaxStackLevel`, `ChargingScheduleAllowedChargingRateUnit` and
  `ChargingScheduleMaxPeriods` here to confirm the charge-power slider can work — smart
  charging is optional in OCPP 1.6.
- Reading all keys is harmless and non-disruptive.
