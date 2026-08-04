# RemoteStopTransaction

Stops an ongoing transaction by its transaction id.

## Request

| Field | Type | Required | Values / notes |
| --- | --- | --- | --- |
| `transactionId` | integer | yes | Id of the running transaction. |

## Response

| Field | Type | Values |
| --- | --- | --- |
| `status` | enum | `Accepted`, `Rejected` |

## Notes

- **This app does not use RemoteStop in normal operation** — stopping is a Home Assistant
  service call. Using this bypasses the orchestrator's state machine.
- Transaction ids in this system are minted locally by the backend
  (`ChargePointSession`), not by the charger. Read the current id from the live log's
  `StartTransaction` result, or from the History page.
- `Rejected` usually means the id is unknown to the charger (already stopped, or a stale id
  after a charger reboot).
- Expect a `StopTransaction` frame in the live log if it worked.
