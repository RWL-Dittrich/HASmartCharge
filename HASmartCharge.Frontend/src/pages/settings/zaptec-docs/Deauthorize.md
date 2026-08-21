# Deauthorize and stop — command 10001

`POST /api/chargers/{id}/sendCommand/10001`. Ends the session by revoking its authorization —
a real session end, unlike the 506 pause.

## Request

No body.

## Notes

> Zaptec's docs: the caller must prevent new charging sessions until the command completes,
> otherwise the car may immediately re-authorize and start a fresh session.

- Use 506/507 for pause/resume; use this only to genuinely terminate a session remotely.
