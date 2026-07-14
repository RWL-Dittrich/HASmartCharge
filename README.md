# HASmartCharge

Charge **one EV as cheaply as possible by a deadline**. HASmartCharge picks the cheapest EPEX
hourly electricity prices, toggles charging via **Home Assistant service calls**, and uses your
**OCPP 1.6J charger as a telemetry source** to measure delivered kWh and actual cost. It never
sends OCPP start/stop commands — starting and stopping charging always goes through Home
Assistant, so it works with whatever actuator you already use (switch, button, script, ...).

Single car, single charger, no multi-tenancy — built to solve one problem well.

## What it can do

- **Automatic cheapest-hours scheduling** — set a weekly departure deadline per day (plus one-off
  date overrides), and it picks the cheapest EPEX hours before that deadline so the car is full in
  time.
- **Live dashboard** — current price, active/next charge window, delivered energy, running cost,
  and a manual override to force charging on/off outside the schedule.
- **Charge-power slider** — cap delivered power in kW from the dashboard; converted to an amp limit
  and pushed to the charger via OCPP `SetChargingProfile` (needs a charger with smart-charging
  support).
- **Cost & session history** — every charging session is recorded with metered kWh and the actual
  €/kWh cost per hour, split proportionally across UTC clock-hours.
- **OCPP 1.6J telemetry server** — a from-scratch central system implementation (BootNotification,
  StatusNotification, MeterValues, StartTransaction/StopTransaction, Heartbeat, ...) that only
  reads meter data; any 1.6J-compliant charger works, no id-tag whitelist required.
- **Home Assistant integration** — reads car state-of-charge from any HA entity, starts/stops
  charging via any HA service call, and (outside the add-on) connects over OAuth2.
- **Runs as a Home Assistant add-on** with sidebar UI (ingress), or standalone via Docker/`dotnet
  run` for development.

## Install

### As a Home Assistant add-on (recommended)

1. In Home Assistant: **Settings → Add-ons → Add-on Store → ⋮ (top right) → Repositories**.
2. Add repository: `https://github.com/RWL-Dittrich/HASmartCharge`
3. Find **HASmartCharge** in the store and click **Install** (pulls a prebuilt image from GHCR).
4. **Start** the add-on. Enable *Start on boot* / *Watchdog* if you like.
5. Open it from the **Smart Charge** entry in the HA sidebar.

The add-on image is published by CI whenever a `v*` tag is pushed; the GHCR package must be public
(or the HA host logged in to GHCR) for the Supervisor to pull it. See
[`addon/DOCS.md`](addon/DOCS.md) for the full add-on guide, including the OCPP raw-frame debug log.

### Standalone with Docker

```bash
docker build -t hasmartcharge .
docker run -d \
  -p 8099:8099 -p 8180:8180 \
  -v hasmartcharge-data:/data \
  hasmartcharge
```

- `8099` — HTTP UI + API.
- `8180` — OCPP 1.6J WebSocket (the charger connects here directly).
- `/data` — persistent volume for the SQLite database (`hasmartcharge.db`).

Outside the add-on there's no Supervisor token, so you connect to Home Assistant via OAuth2 (see
Setup below).

### From source (development)

Requires .NET 10 SDK and Node 22.

```powershell
dotnet build HASmartCharge.slnx                # full build
dotnet test HASmartCharge.Core.Tests           # unit tests
dotnet run --project HASmartCharge.Backend     # backend API on http://localhost:5293
```

```bash
cd HASmartCharge.Frontend
npm install
npm run dev                                    # Vite dev server on :5173, proxies /api to the backend
```

Or run both together with .NET Aspire, which wires the frontend's backend URL automatically:

```powershell
dotnet run --project HASmartCharge.AppHost
```

## Setup

1. **Connect Home Assistant**
   - **Add-on**: nothing to do — it authenticates with the Supervisor token and talks to your HA
     Core automatically.
   - **Standalone**: open **Settings → Home Assistant**, enter your HA base URL, and click
     **Connect**. You're redirected to HA to authorize the app, then back.
2. **Configure the car** — on **Settings → Car**, pick the HA entity for state-of-charge and the
   HA service calls used to start/stop charging (any domain/service works — switch, button,
   script...).
3. **Configure the charger** — on **Settings → Charger**, set the charge point id, phase count,
   supply voltage, and min/max charge power. Point your charger's OCPP 1.6J central-system URL at:

   ```
   ws://<host>:8180/ocpp/1.6/<chargePointId>
   ```

   using the **same** `<chargePointId>` you set here. The charger is telemetry-only — it reports
   meter values and transaction status, and never receives start/stop commands.
4. **Configure the price provider** — on **Settings → Price Provider**, set the refresh interval
   for EPEX hourly prices (tomorrow's prices publish around 13:00 CET).
5. **Set a schedule** — on the **Schedule** page, set a weekly departure time per day (with
   optional one-off date overrides). The dashboard shows the resulting cheapest-hours plan and
   updates it every minute.

## Tech stack

.NET 10 / ASP.NET Core · Entity Framework Core (SQLite) · a from-scratch OCPP 1.6J WebSocket
server · Home Assistant REST + OAuth2 · React 19 / Vite / TypeScript / TanStack Query / Tailwind v4.
