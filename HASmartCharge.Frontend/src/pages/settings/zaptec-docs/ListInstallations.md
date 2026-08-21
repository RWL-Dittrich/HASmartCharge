# List installations

`GET /api/installation` — every installation the account can see. Paged like `/api/chargers`
(`PageSize`, `PageIndex`).

## Response

`{ pages, totalCount, data: [...] }`; each entry carries `id`, `name`, address fields,
`maxCurrent`, `availableCurrent`, and network/phase metadata.

## Notes

- The installation `id` is what the *Installation details / hierarchy / update* templates need
  in place of `{installationId}`.
- Your charger's `installationId` is in *Charger details* and in the *List chargers* rows.
