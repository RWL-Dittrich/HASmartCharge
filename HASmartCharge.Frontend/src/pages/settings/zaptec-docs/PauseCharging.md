# Pause charging — command 506

`POST /api/chargers/{id}/sendCommand/506` (Zaptec name: *StopChargingFinal*). Pauses the
running session: the charger stops delivering power, sets `FinalStopActive (718) = 1`, and the
session (and cable lock) stays engaged. Fully resumable with command 507.

## Request

No body.

## Preconditions

- Meaningful when `ChargerOperationMode (710) = 3` (charging).
- Rejected if the charger is already paused or disconnected.
- Requires firmware > 3.2 on Pro chargers.

## Notes

- This is what HASmartCharge sends for "stop" when charge control mode is *Charger* — it is a
  pause, not a session end, so cheap-hour toggling resumes the same session.
- Zaptec's docs recommend this command pair over `MaxCurrent` manipulation for pause/resume:
  it is deterministic and works for offline chargers when they reconnect.
