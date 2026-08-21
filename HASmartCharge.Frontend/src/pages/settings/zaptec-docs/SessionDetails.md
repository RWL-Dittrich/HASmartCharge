# Session details

`GET /api/session/{id}` — one charging session by its UUID.

## Response highlights

| Field | Meaning |
| --- | --- |
| `id` | Session UUID (observation 721 while running) |
| `chargerId`, `deviceName` | Which charger |
| `startDateTime`, `endDateTime` | Session window (UTC) |
| `energy` | Delivered energy (kWh) |
| `commitMetadata`, `signedSession` | OCMF signing info where enabled |

## Notes

- The running session's UUID is observation `721` in *Charger state*; HASmartCharge derives
  its local session id from that UUID.
