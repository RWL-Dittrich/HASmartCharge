# Resume charging — command 507

`POST /api/chargers/{id}/sendCommand/507`. Resumes a session paused with command 506.

## Request

No body.

## Preconditions

- Meaningful when `ChargerOperationMode (710) = 5` **and** `FinalStopActive (718) = 1`.
- Rejected if the charger is not paused or a scheduler is active.
- Requires firmware > 3.2 on Pro chargers.

## Notes

> `710 = 5` with `FinalStopActive = 0` means the **car** ended charging itself (battery full or
> car-side schedule) — resume does nothing useful there, and HASmartCharge's orchestrator
> deliberately skips it in that state.
