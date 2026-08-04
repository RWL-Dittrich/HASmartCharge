# GetLocalListVersion

Reads the version number of the charger's local authorization list. Takes no fields — send `{}`.

## Request

_No fields._

## Response

| Field | Type | Notes |
| --- | --- | --- |
| `listVersion` | integer | Current version. `0` = the list is empty. `-1` = local authorization is not supported. |

## Notes

- Read-only and non-disruptive.
- This app does not use the local list: inbound transactions are auto-accepted with no id-tag
  whitelist, by design.
- Check the `LocalAuthListEnabled` configuration key before assuming a charger honours the list.
