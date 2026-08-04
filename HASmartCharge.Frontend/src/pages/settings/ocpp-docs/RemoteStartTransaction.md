# RemoteStartTransaction

Asks the charger to start a transaction for the given `idTag`, as if that token had been
presented locally. Whether the charger actually starts charging depends on its
`AuthorizeRemoteTxRequests` configuration key and on the connector state.

## Request

| Field | Type | Required | Values / notes |
| --- | --- | --- | --- |
| `connectorId` | integer | no | `> 0`. Omit to let the charger pick a connector. |
| `idTag` | string (20) | yes | Authorization token the transaction is booked against. |
| `chargingProfile` | ChargingProfile | no | Must use `chargingProfilePurpose: "TxProfile"`. See **SetChargingProfile** for the object shape. |

## Response

| Field | Type | Values |
| --- | --- | --- |
| `status` | enum | `Accepted`, `Rejected` |

## Notes

- **This app does not use RemoteStart in normal operation.** Charging start/stop runs through
  Home Assistant service calls; the OCPP link is telemetry only. Sending this by hand puts the
  charger in a state the orchestrator did not ask for, and the next orchestrator tick may stop
  it again via Home Assistant.
- `Accepted` means the request was accepted, not that charging began. Watch for the following
  `StartTransaction` in the live log to confirm.
- If `AuthorizeRemoteTxRequests` is `true`, the charger sends `Authorize` first.
