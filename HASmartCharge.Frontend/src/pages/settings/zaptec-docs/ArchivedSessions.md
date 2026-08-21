# Archived sessions

`GET /api/sessions/archived` — completed sessions, cursor-paginated.

## Query parameters

| Param | Meaning |
| --- | --- |
| `ChargerId` | Filter to one charger (pre-filled by the template) |
| `InstallationId` | Filter to an installation |
| `From` / `To` | UTC window |
| `Cursor` | Continuation cursor from the previous page |

## Response

`{ data: [...], cursor }` — pass `cursor` back to page. Each row resembles *Session details*
(id, charger, start/end, energy).

## Notes

- Replaces the deprecated `/api/chargehistory` endpoint.
