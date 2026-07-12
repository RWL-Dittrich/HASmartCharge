# HASmartCharge — setup

## Install

1. In Home Assistant: **Settings → Add-ons → Add-on Store → ⋮ (top right) → Repositories**.
2. Add: `https://github.com/RWL-Dittrich/HASmartCharge`
3. The **HASmartCharge** add-on appears in the store — click **Install** (it pulls a prebuilt
   image from GHCR, so install is quick).
4. **Start** the add-on. Enable *Start on boot* and *Watchdog* if you like.
5. Open the UI from the **Smart Charge** entry in the HA sidebar.

> The add-on image is published to GitHub Container Registry by the repo's CI when a
> `v*` release tag is pushed. The GHCR package must be **public** (or the HA host logged in to
> GHCR) for the Supervisor to pull it.

## Home Assistant connection

Running as an add-on, HASmartCharge authenticates with the **Supervisor token** and talks to your
HA Core at `http://supervisor/core`. There is **no OAuth/connect step** — it's already connected to
the HA instance it runs in. Configure the car's SoC entity and the start/stop service calls on the
**Settings → Car** tab in the app UI.

## Connect your charger (OCPP 1.6J)

The charger connects **directly** to the add-on (not through the sidebar/ingress). Point its OCPP
1.6J central-system URL at:

```
ws://<HA-IP>:8180/ocpp/1.6/<chargePointId>
```

- `<HA-IP>` — your Home Assistant host's IP/hostname.
- `<chargePointId>` — any id; set the **same** value on the **Settings → Charger** tab.
- Port `8180` is exposed by the add-on (change it under the add-on's **Network** settings if needed).

The charger is used for **telemetry only** (kWh + cost). Charging start/stop is done via your HA
service calls, never via OCPP.

## Data & updates

The SQLite database lives in the add-on's `/data` volume, so schedules, settings, prices and
session history **survive add-on updates and restarts**. Database migrations apply automatically on
start.

## Notes

- All times are handled in UTC internally; departure times are entered in local wall-clock and
  resolved via the configured time zone (default `Europe/Amsterdam`).
- Tomorrow's EPEX prices publish around 13:00 CET; the schedule fills in once they're available.
