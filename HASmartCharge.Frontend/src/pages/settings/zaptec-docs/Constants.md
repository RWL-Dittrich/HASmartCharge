# Constants

`GET /api/constants` — the API's machine-readable dictionary: every observation id
(`Observations`), command id (`Commands`), device type, operation mode and error code enum the
current API version knows.

## Notes

- Big response (hundreds of entries) — the result panel scrolls.
- This is the authoritative source when an observation id from *Charger state* is not in the
  table documented there; ids vary by charger model.
