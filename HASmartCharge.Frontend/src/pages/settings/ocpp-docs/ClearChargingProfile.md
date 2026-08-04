# ClearChargingProfile

Removes installed charging profiles. Every field is a filter, and every field is optional — an
empty payload `{}` clears **all** profiles on the charge point.

## Request

| Field | Type | Required | Values / notes |
| --- | --- | --- | --- |
| `id` | integer | no | A specific `chargingProfileId`. When given, the other filters are ignored. |
| `connectorId` | integer | no | `0` matches profiles installed on the charge point itself. |
| `chargingProfilePurpose` | enum | no | `ChargePointMaxProfile`, `TxDefaultProfile`, `TxProfile` |
| `stackLevel` | integer | no | Only profiles at exactly this level. |

## Response

| Field | Type | Values |
| --- | --- | --- |
| `status` | enum | `Accepted`, `Unknown` |

## Notes

- `Unknown` means no profile matched the filter — not an error.
- Clearing the app's `TxDefaultProfile` removes the current-limit cap, so the car may draw full
  power until the next slider change re-installs it.
- Note the enum: there is no `Rejected` here.
