# Restart charger — command 102

`POST /api/chargers/{id}/sendCommand/102`. Reboots the charger.

## Request

No body.

## Notes

- An active session is interrupted; most chargers resume charging automatically after boot if
  the car still requests power.
- The charger drops offline for a minute or two — expect `IsOnline (-2) = 0` and stale
  observations until it reconnects to the Zaptec cloud.
