# Update installation

`POST /api/installation/{id}/update` — writes installation-level settings; the relevant one is
the load-balancer input.

## Body fields

| Field | Type | Meaning |
| --- | --- | --- |
| `availableCurrent` | number (A) | Current the load balancer may distribute across all chargers |
| `availableCurrentPhase1/2/3` | number (A) | Per-phase variant (mutually exclusive with the single value) |

## Notes

> Zaptec's docs: **do not update this more than once every 15 minutes** — it is persisted to
> the chargers, and abusing it wears flash and fights the balancer.

- This is Zaptec's recommended lever for slow current steering across multiple chargers; for a
  single home charger HASmartCharge uses the charger's own `maxChargeCurrent` instead so the
  dashboard slider is not rate-limited.
