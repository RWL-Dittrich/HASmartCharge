# ReserveNow

Reserves a connector for one token until an expiry moment. A reserved connector refuses other
tokens and reports `Reserved` in StatusNotification.

## Request

| Field | Type | Required | Values / notes |
| --- | --- | --- | --- |
| `connectorId` | integer | yes | `0` = any connector, if the charger supports that. |
| `expiryDate` | dateTime | yes | ISO-8601. When the reservation lapses. |
| `idTag` | string (20) | yes | The token the reservation is held for. |
| `parentIdTag` | string (20) | no | Any token in this group may use the reservation. |
| `reservationId` | integer | yes | Your id, used later by **CancelReservation**. |

## Response

| Field | Type | Values |
| --- | --- | --- |
| `status` | enum | `Accepted`, `Faulted`, `Occupied`, `Rejected`, `Unavailable` |

## Notes

- `Rejected` typically means the charger has reservations disabled — check the
  `ReserveConnectorZeroSupported` and `ReservationEnabled`-style keys via **GetConfiguration**.
- `Occupied` = a transaction is running; `Unavailable` = the connector is `Inoperative`.
- Reserving the connector this app uses will block scheduled charging until the reservation
  expires or is cancelled. The template's `expiryDate` defaults to one hour out.
- The example payload's timestamp is generated when you pick the template.
