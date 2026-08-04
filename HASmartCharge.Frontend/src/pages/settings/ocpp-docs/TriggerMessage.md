# TriggerMessage

Asks the charger to send a specific message right now, instead of waiting for its normal
trigger. The safest call on this page: it only requests telemetry.

## Request

| Field | Type | Required | Values / notes |
| --- | --- | --- | --- |
| `requestedMessage` | enum | yes | `BootNotification`, `DiagnosticsStatusNotification`, `FirmwareStatusNotification`, `Heartbeat`, `MeterValues`, `StatusNotification` |
| `connectorId` | integer | no | `> 0`. Only meaningful for `MeterValues` and `StatusNotification`. Omit for charge-point-wide messages. |

## Response

| Field | Type | Values |
| --- | --- | --- |
| `status` | enum | `Accepted`, `Rejected`, `NotImplemented` |

## Notes

- Good for verifying the link end to end: the outbound CALL, the charger's CALLRESULT, and the
  triggered message itself all appear in the live log.
- `NotImplemented` means the charger does not support triggering that particular message.
- `MeterValues` is the quickest way to refresh live power/energy without waiting for the next
  sample interval.
