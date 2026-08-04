# ChangeAvailability

Takes a connector (or the whole charge point) in or out of service. An `Inoperative` connector
refuses new transactions and normally reports `Unavailable` in StatusNotification.

## Request

| Field | Type | Required | Values / notes |
| --- | --- | --- | --- |
| `connectorId` | integer | yes | `0` = the entire charge point, `> 0` = one connector. |
| `type` | enum | yes | `Operative`, `Inoperative` |

## Response

| Field | Type | Values |
| --- | --- | --- |
| `status` | enum | `Accepted`, `Rejected`, `Scheduled` |

## Notes

- `Scheduled` means a transaction is currently running: the change applies once it ends. The
  charger does **not** interrupt charging for this.
- Same call the dashboard availability toggle uses
  (`POST /api/charger/availability` → `ChargerControl.SetConnectorAvailabilityAsync`), which
  always targets the configured connector. Use this page only when you need `connectorId: 0`.
- Leaving a connector `Inoperative` blocks charging silently — the scheduler will keep planning
  hours that can never deliver energy.
