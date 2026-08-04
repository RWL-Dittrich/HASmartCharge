# ChangeConfiguration

Writes a single configuration key. Values are **always strings on the wire**, even for numbers
and booleans (`"60"`, `"true"`).

## Request

| Field | Type | Required | Values / notes |
| --- | --- | --- | --- |
| `key` | string (50) | yes | Key name. Case-sensitive. |
| `value` | string (500) | yes | New value, as a string. |

## Response

| Field | Type | Values |
| --- | --- | --- |
| `status` | enum | `Accepted`, `Rejected`, `RebootRequired`, `NotSupported` |

## Notes

- `NotSupported` = unknown key. `Rejected` = known but read-only or an invalid value.
  `RebootRequired` = stored, but only takes effect after a `Reset`.
- The backend already pushes its keys on every connect
  (`ChargerConfigurationService`), and skips keys already at the desired value. Changing one of
  those by hand will be overwritten on the next charger reconnect — change it on the charger
  settings tab instead if you want it to stick.
- `HeartbeatInterval` also drives the backend's dead-link detection window
  (`max(3 × interval, 90s)`), so a large value here delays disconnect detection.
