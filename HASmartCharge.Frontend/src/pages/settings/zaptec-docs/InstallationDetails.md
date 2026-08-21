# Installation details

`GET /api/installation/{id}` — full record for one installation: address, grid/phase type,
`maxCurrent`, `availableCurrent`, authorization settings, and messaging metadata.

## Notes

- `availableCurrent` here is the load-balancer input that *Update installation* writes.
- `maxCurrent` is the physical fuse limit — `availableCurrent` above it has no effect.
