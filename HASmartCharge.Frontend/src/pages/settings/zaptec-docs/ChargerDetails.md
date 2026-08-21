# Charger details

`GET /api/chargers/{id}` — full record for one charger.

## Response highlights

| Field | Meaning |
| --- | --- |
| `id`, `name`, `deviceId` | Identity |
| `installationId`, `circuitId` | Where it hangs in the installation hierarchy |
| `isOnline`, `operatingMode` | Live connectivity + mode (1/2/3/5) |
| `deviceType` | Hardware family (Pro, Go, …) |
| `active` | Whether the charger is enabled |
| `pin` | Device PIN |

## Notes

- Settings values (currents, phases) live in the charger *state* observations and the
  installation record, not here — this is mostly static identity data.
