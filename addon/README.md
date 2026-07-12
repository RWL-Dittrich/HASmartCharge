# HASmartCharge

Charge **one EV as cheaply as possible by a deadline**. HASmartCharge picks the cheapest EPEX
hourly prices, toggles charging via **Home Assistant service calls**, and uses your **OCPP 1.6J
charger as a telemetry source** to measure delivered kWh and actual cost.

- **Automatic schedules** — set a weekly departure time per day; plug in and it arms a plan to be
  full by your next departure at the cheapest hours. One-off date overrides for days off.
- **Runs inside Home Assistant** — uses the Supervisor token, so there's no separate HA login or
  "connect" step. It talks to your HA Core directly.
- **UI in the sidebar** — opens via HA ingress (behind your HA login).

## At a glance

| | |
|---|---|
| UI | HA sidebar → **Smart Charge** (ingress) |
| Charger connects to | `ws://<HA-IP>:8180/ocpp/1.6/<chargePointId>` |
| Data | SQLite in the add-on's `/data` (survives updates) |
| Architectures | amd64 |

See the **Documentation** tab for install and charger setup.
