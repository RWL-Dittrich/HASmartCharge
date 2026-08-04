# Reset

Restarts the charge point. A `Hard` reset is equivalent to a power cycle: any ongoing
transaction is stopped and **not** cleanly ended first. A `Soft` reset restarts the
application, stopping ongoing transactions normally (StopTransaction is sent) where the
charger supports it.

## Request

| Field | Type | Required | Values |
| --- | --- | --- | --- |
| `type` | enum | yes | `Hard`, `Soft` |

## Response

| Field | Type | Values |
| --- | --- | --- |
| `status` | enum | `Accepted`, `Rejected` |

## Notes

- The charger drops the WebSocket and re-sends `BootNotification` after the reset, so expect a
  reconnect in the live log. The on-connect config push runs again.
- `Accepted` only means the reset was scheduled — it says nothing about the charger coming back.
