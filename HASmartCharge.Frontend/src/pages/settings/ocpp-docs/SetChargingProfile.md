# SetChargingProfile

Installs a charging profile that **caps** the delivered current or power. It never starts or
stops a transaction. This is the call behind the dashboard charge-power slider.

## Request

| Field | Type | Required | Values / notes |
| --- | --- | --- | --- |
| `connectorId` | integer | yes | `0` only for `ChargePointMaxProfile`; otherwise the target connector. |
| `csChargingProfiles` | ChargingProfile | yes | See below. |

**ChargingProfile**

| Field | Type | Required | Values / notes |
| --- | --- | --- | --- |
| `chargingProfileId` | integer | yes | Your id. Re-sending the same id replaces that profile. |
| `transactionId` | integer | no | Only for `TxProfile`; binds the profile to one transaction. |
| `stackLevel` | integer | yes | `0` = lowest. Higher levels win. Must not exceed the charger's `ChargeProfileMaxStackLevel`. |
| `chargingProfilePurpose` | enum | yes | `ChargePointMaxProfile`, `TxDefaultProfile`, `TxProfile` |
| `chargingProfileKind` | enum | yes | `Absolute`, `Recurring`, `Relative` |
| `recurrencyKind` | enum | no | `Daily`, `Weekly`. Only with `Recurring`. |
| `validFrom` / `validTo` | dateTime | no | Profile validity window. |
| `chargingSchedule` | ChargingSchedule | yes | See below. |

**ChargingSchedule**

| Field | Type | Required | Values / notes |
| --- | --- | --- | --- |
| `duration` | integer | no | Schedule length in seconds. |
| `startSchedule` | dateTime | no | Required for `Absolute`/`Recurring`, must be absent for `Relative`. |
| `chargingRateUnit` | enum | yes | `A` (amps) or `W` (watts). Check `ChargingScheduleAllowedChargingRateUnit`. |
| `minChargingRate` | decimal | no | Lowest rate the charger should use, one decimal. |
| `chargingSchedulePeriod` | array of period | yes | At least one; at most `ChargingScheduleMaxPeriods`. |

**chargingSchedulePeriod entry**

| Field | Type | Required | Values / notes |
| --- | --- | --- | --- |
| `startPeriod` | integer | yes | Seconds from the schedule start. First entry is `0`. |
| `limit` | decimal | yes | The cap, in `chargingRateUnit`. One decimal. |
| `numberPhases` | integer | no | Defaults to 3. |

## Response

| Field | Type | Values |
| --- | --- | --- |
| `status` | enum | `Accepted`, `Rejected`, `NotSupported` |

## Notes

- The app sends a `TxDefaultProfile` / `Relative` profile in **amps**, converting kW with
  `A = W / (phases × voltage)` rounded down, because most OCPP 1.6 chargers cap current rather
  than power. See `POST /api/charger/power`.
- Sending a profile by hand here does **not** update the stored `ChargePowerSetpointKw`, so the
  dashboard slider will disagree with reality until the next slider change.
- `NotSupported` means the charger has no smart-charging support at all — the slider cannot
  work on that hardware.
- A profile at a higher `stackLevel` overrides the app's profile. Use **ClearChargingProfile**
  to remove anything you install here.
