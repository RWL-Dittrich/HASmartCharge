# GetCompositeSchedule

Asks the charger what limit it will actually apply over a period, after merging every installed
profile by stack level. The way to verify a profile you sent is really in effect.

## Request

| Field | Type | Required | Values / notes |
| --- | --- | --- | --- |
| `connectorId` | integer | yes | `0` = the whole charge point. |
| `duration` | integer | yes | Length of the requested schedule, in seconds. |
| `chargingRateUnit` | enum | no | `A` or `W`. Omit to let the charger choose. |

## Response

| Field | Type | Notes |
| --- | --- | --- |
| `status` | enum | `Accepted`, `Rejected` |
| `connectorId` | integer | Echoed connector. |
| `scheduleStart` | dateTime | Absent when the charger returns a relative schedule. |
| `chargingSchedule` | ChargingSchedule | Same shape as in **SetChargingProfile**. Absent when `Rejected`. |

## Notes

- The charger may return a shorter period than requested.
- Read-only and non-disruptive.
- If the returned `limit` differs from what the slider set, something is overriding it — check
  for a `ChargePointMaxProfile` or a higher `stackLevel` profile.
