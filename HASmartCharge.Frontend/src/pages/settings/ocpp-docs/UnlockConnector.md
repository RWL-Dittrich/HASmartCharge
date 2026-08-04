# UnlockConnector

Releases the cable lock on a connector, for a cable stuck in the socket. It does **not** stop a
transaction and is not a way to end charging.

## Request

| Field | Type | Required | Values / notes |
| --- | --- | --- | --- |
| `connectorId` | integer | yes | `> 0`. `0` is not allowed. |

## Response

| Field | Type | Values |
| --- | --- | --- |
| `status` | enum | `Unlocked`, `UnlockFailed`, `NotSupported` |

## Notes

- `NotSupported` is normal for chargers with a fixed (tethered) cable — there is nothing to
  unlock.
- Same call as the dashboard unlock button (`POST /api/charger/unlock`).
- Note the response uses its own enum: there is no `Accepted` here.
