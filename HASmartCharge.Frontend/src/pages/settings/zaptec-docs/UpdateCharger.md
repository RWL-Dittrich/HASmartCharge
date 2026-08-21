# Update charger

`POST /api/chargers/{id}/update` — writes charger-local settings (`ChargerExternalUpdateModel`).

## Body fields

| Field | Type | Range | Meaning |
| --- | --- | --- | --- |
| `maxChargeCurrent` | number (A) | 0–32 | Upper limit for charge current; below ~6 A effectively prevents charging |
| `minChargeCurrent` | number (A) | 0–32 | Minimum allocated current (default 6) |
| `maxChargePhases` | 1 or 3 | | Max phases the charger may use |
| `offlineChargeCurrent` | number (A) | 0–32, −1 | Current when cloud is unreachable; −1 = automatic |
| `offlineChargePhase` | enum | 0,1,2,4,7 | Offline phase selection (0 auto, 7 all) |
| `meterValueInterval` | int (s) | ≥0 | Reporting frequency (Zaptec recommends 1800) |

## Notes

- The dashboard power slider uses `maxChargeCurrent` here when the charger type is Zaptec.
- Zaptec's docs prefer steering via the installation's `availableCurrent` to avoid fighting
  the load balancer — fine to ignore for a single-charger home installation, and the
  installation update is rate-limited to one change per 15 minutes.
