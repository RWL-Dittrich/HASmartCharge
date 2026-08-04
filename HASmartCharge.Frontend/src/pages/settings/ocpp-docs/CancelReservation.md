# CancelReservation

Cancels a reservation made with **ReserveNow**.

## Request

| Field | Type | Required | Values / notes |
| --- | --- | --- | --- |
| `reservationId` | integer | yes | The id you passed to ReserveNow. |

## Response

| Field | Type | Values |
| --- | --- | --- |
| `status` | enum | `Accepted`, `Rejected` |

## Notes

- `Rejected` means no reservation with that id exists (already used, expired, or wrong id).
- The connector returns to `Available` and scheduled charging can proceed again.
