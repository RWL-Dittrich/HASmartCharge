# Installation hierarchy

`GET /api/installation/{id}/hierarchy` — the installation's circuit tree: circuits with their
fuse ratings, and the chargers hanging on each circuit.

## Notes

- Useful to see how Zaptec's load balancer divides `availableCurrent` across chargers on the
  same circuit.
