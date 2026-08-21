# Charger state (observations)

`GET /api/chargers/{id}/state` — every current state observation for the charger. This is the
exact call the HASmartCharge poller runs on your poll interval.

## Response

An array of `{ stateId, timestamp, valueAsString }`. The interesting ids:

| StateId | Name | Meaning |
| --- | --- | --- |
| `-2` | IsOnline | 1 = online, 0 = offline |
| `501–503` | VoltagePhase1–3 | Output voltage per phase (V) |
| `507–509` | CurrentPhase1–3 | Output current per phase (A) |
| `513` | TotalChargePower | Instant charge power (W) |
| `553` | TotalChargePowerSession | Energy delivered in the current session (kWh) |
| `554` | SignedMeterValue | Signed (OCMF) meter reading |
| `710` | ChargerOperationMode | 1 Disconnected, 2 Requesting, 3 Charging, 5 Finished/Paused |
| `718` | FinalStopActive | 1 = paused by command 506, resumable with 507 |
| `721` | SessionIdentifier | Current session UUID |
| `911` | SoftwareApplicationVersion | Firmware version |

## Notes

- State ids vary by charger model; `GET /api/constants` lists the full `Observations` enum.
- Timestamps can be stale for values that have not changed recently — the poller stamps its
  own UTC time on samples for exactly that reason.
