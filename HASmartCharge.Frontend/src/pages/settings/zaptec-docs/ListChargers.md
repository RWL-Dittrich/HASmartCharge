# List chargers

Returns every charger the account can see, matching the provided filters. Paged.

## Query parameters

| Param | Type | Notes |
| --- | --- | --- |
| `PageSize` | int | Items per page, max 100, default 50 |
| `PageIndex` | int | Page number, starting at 0 |
| `InstallationId` | UUID | Filter by installation |
| `NameFilter` | string | Search by name |
| `IncludeDisabled` | bool | Include disabled chargers |

## Response

`{ pages, totalCount, data: [...] }` where each entry carries:

| Field | Meaning |
| --- | --- |
| `id` | Charger UUID — the value HASmartCharge stores as the Zaptec charger id |
| `name` | Display name |
| `deviceId` | Serial number (e.g. ZAP012345) |
| `installationId` | Parent installation UUID |
| `isOnline` | Cloud connectivity |
| `operatingMode` | 0 Unknown, 1 Disconnected, 2 Connected_Requesting, 3 Connected_Charging, 5 Connected_Finished |

## Notes

- This is the same call the charger picker on the Charger settings tab uses (first page only).
