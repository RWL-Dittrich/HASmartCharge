# ClearCache

Empties the charger's authorization cache — the locally remembered results of past `Authorize`
calls. Takes no fields; send `{}`.

## Request

_No fields._

## Response

| Field | Type | Values |
| --- | --- | --- |
| `status` | enum | `Accepted`, `Rejected` |

## Notes

- `Rejected` usually means the charger has no authorization cache, or has it disabled
  (`AuthorizationCacheEnabled`).
- Harmless here: this app auto-accepts every inbound transaction, so a cold cache changes
  nothing about whether charging is allowed.
- Does not touch the local authorization list — that is **SendLocalList**.
