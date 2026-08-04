# SendLocalList

Sends or updates the charger's local authorization list, so it can authorize tokens while
offline.

## Request

| Field | Type | Required | Values / notes |
| --- | --- | --- | --- |
| `listVersion` | integer | yes | New version number. Must be higher than the current one. |
| `updateType` | enum | yes | `Full` replaces the list, `Differential` applies only the entries sent. |
| `localAuthorizationList` | array of AuthorizationData | no | Omit with `Full` to clear the list. |

**AuthorizationData**

| Field | Type | Required | Values / notes |
| --- | --- | --- | --- |
| `idTag` | string (20) | yes | The token. |
| `idTagInfo` | IdTagInfo | no | Omit in a `Differential` update to **delete** this entry. |

**IdTagInfo**

| Field | Type | Required | Values / notes |
| --- | --- | --- | --- |
| `status` | enum | yes | `Accepted`, `Blocked`, `Expired`, `Invalid`, `ConcurrentTx` |
| `expiryDate` | dateTime | no | After this, the token is treated as expired. |
| `parentIdTag` | string (20) | no | Groups tokens (e.g. a card and its backup). |

## Response

| Field | Type | Values |
| --- | --- | --- |
| `status` | enum | `Accepted`, `Failed`, `NotSupported`, `VersionMismatch` |

## Notes

- `VersionMismatch` means your `listVersion` is not higher than the stored one — check with
  **GetLocalListVersion** first.
- Check `LocalAuthListEnabled` and `SendLocalListMaxLength` before sending a large list.
- This app never sends this call: transactions are auto-accepted, no whitelist.
