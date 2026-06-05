# HASmartCharge — Backend + Frontend Rewrite Plan

## 1. Goal

Strip the project down to one focused job:

> Charge **one** EV as cheaply as possible so it's **full (or at a target %) by a deadline**, by pressing Home Assistant start/stop buttons during the cheapest electricity hours — using EPEX hourly prices, and using the OCPP charger purely to measure delivered energy and compute what the charge actually cost.

Concretely:

- Accept a connection from **one OCPP 1.6J charger** (more allowed by schema, UI assumes one). OCPP is **read-only telemetry** — we never send it control commands.
- Control the **car** via Home Assistant: a start button/service and a stop button/service, plus a battery-% sensor.
- Pull **hourly prices** from `https://epexprijzen.nl/api/v1/prices/nextenergy/hourly` (supplier slug configurable in the UI), cache them, and graph them.
- Let the user configure **car settings** (battery capacity, target %, HA entity/service IDs) and **charger settings** (max charge kW, charge point ID).
- Let the user set a **deadline** ("full by …"). A scheduler picks the cheapest hours before the deadline and toggles HA start/stop so the car is ready in time.
- Show **actual cost**: OCPP meter energy × the EPEX price of the hour it was delivered in.

### Decisions locked (from requirements review)

| Topic | Decision |
|---|---|
| Charging start/stop | **Home Assistant service calls only.** No OCPP RemoteStart/Stop. |
| Charger max kW | **Config value, used for scheduling math only.** No OCPP SetChargingProfile / current limiting. |
| OCPP role | **Mostly telemetry** (connection, status, meter values → kWh + cost), **plus a few kept control commands**: unlock connector, set connector available/inoperative, and an **on-connect config push** (meter-value intervals, sampled measurands, heartbeat interval). |
| Auto-accept | **All inbound transactions auto-accepted** — `BootNotification`, `Authorize`, `StartTransaction`, `StopTransaction` always return `Accepted` (already coded; keep it). No id-tag whitelist. |
| HA auth | **Keep existing OAuth2** (indieauth flow, refresh-token rotation, token-refresh background service). |
| OCPP code | **Keep transport/session/router + inbound handlers + the outbound send/correlation infra. Keep only: ChangeConfiguration, GetConfiguration, TriggerMessage, ChangeAvailability, UnlockConnector. Strip every other outbound command + unused message.** |

---

## 2. Current state (what exists today)

7 projects, heavily over-engineered for the goal:

- `HASmartCharge.Domain` — DDD entities (`Charger`, `Connector`, `ChargingSession`) + domain events.
- `HASmartCharge.Application` — full CQRS: commands, handlers, event dispatcher, read-model + repository interfaces, query snapshots.
- `HASmartCharge.Backend.DB` — EF Core (SQLite), models `Charger`/`Connector`/`ChargingTransaction`/`HomeAssistantConnection`, repositories, 2 migrations.
- `HASmartCharge.Backend.HomeAssistant` — OAuth2 (auth service, connection manager, in-memory state store, token-refresh + cleanup background services), an API service (`/api/states`), and a **publish-to-HA** gateway (`IHomeAutomationGateway` / `HomeAssistantGateway`) that is mostly TODO stubs.
- `HASmartCharge.Backend.OCPP` — a complete OCPP 1.6J central system: WebSocket transport, session manager, message router, ~25 message models, a `ChargerStatusTracker` that already parses `Energy.Active.Import.Register` (kWh), power, voltage, current, SoC, plus an **outbound** command path (`ICommandSender`, `OcppChargerGateway`, `ChargerConfigurationService`).
- `HASmartCharge.Backend` — ASP.NET Core Web API, controllers for chargers/commands/dashboard/HA-auth/OCPP-websocket.
- `HASmartCharge.Frontend` — React 19 + Vite + TanStack Query + TanStack Table + recharts + Tailwind v4. Pages: Dashboard, Chargers, Analytics.
- `HASmartCharge.AppHost` — .NET Aspire orchestration.

### Verified facts used by this plan

- **EPEX API shape** (fetched live):
  ```json
  {
    "today":    [ { "t": "2026-06-04T22:00:00Z", "price": 0.221715 }, … 24 entries ],
    "tomorrow": [ { "t": "2026-06-05T22:00:00Z", "price": 0.311104 }, … 24 entries ]
  }
  ```
  - `t` = ISO-8601 **UTC** hour start. `price` = all-in **€/kWh** (supplier price incl. tax/markup).
  - `tomorrow` is empty until ~13:00 CET (day-ahead publication). Requires a browser `User-Agent` header (server returns 403 without one).
- **OCPP energy source**: `ChargerStatusTracker.OnMeterValues` already extracts `Energy.Active.Import.Register` and normalizes Wh→kWh. `StartTransaction`/`StopTransaction` carry meter start/stop. This is enough to compute delivered kWh and bucket it per hour.
- **OCPP WebSocket endpoint**: `ws://host:port/ocpp/1.6/{chargePointId}`, sub-protocol `ocpp1.6` (`OcppController`). Unchanged.

---

## 3. Target architecture

Collapse from 7 projects to 6. Kill the DDD/CQRS layer; keep a thin pure-logic core.

```
HASmartCharge.Core            (was Domain) — plain models + pure calculators (scheduling, cost). No events, no CQRS.
HASmartCharge.Backend.OCPP    — transport + session + router + inbound handlers + a slim outbound path (unlock, availability, on-connect config push). Telemetry sink interface. Auto-accepts all transactions.
HASmartCharge.Backend.HomeAssistant — OAuth2 (kept) + HA control service (read SoC, call start/stop services).
HASmartCharge.Backend.DB      — EF Core: settings + prices + plan + sessions.
HASmartCharge.Backend         — Web API: controllers + background services (price fetch, orchestrator, cost).
HASmartCharge.Frontend        — React: Dashboard, Schedule, Settings, History.
HASmartCharge.AppHost         — Aspire (unchanged wiring).
```

**Deleted entirely:** `HASmartCharge.Application` (all CQRS), the domain-events system, the publish-to-HA gateway, and the OCPP outbound command stack.

### Dependency direction

```
Frontend ──HTTP──► Backend ──► Backend.DB
                      │  ├──► Backend.HomeAssistant ──► (HA REST: /api/states, /api/services)
                      │  └──► Backend.OCPP ◄──WS── physical charger
                      └──► Core (pure logic, no deps)
```

---

## 4. What to keep / strip / delete

### `Backend.OCPP` — KEEP
**Transport / routing / session core:**
- `Transport/` (`IConnection`, `WebSocketConnection`)
- `Domain/` (`ISessionManager`, `SessionManager`, `IChargePointSession`, `ChargePointSession`)
- `Application/OcppMessageRouter` + `IOcppMessageRouter`
- `Infrastructure/OcppConnectionOrchestrator`
- `Services/WebSocketMessageService`, `Services/ChargerStatusTracker`

**Outbound infra (required by the config push + kept commands):**
- `Services/ICommandSender` + `SessionCommandSender` (routes a command to a session by charge-point id — used by the config service).
- In `ChargePointSession`: keep `SendCommandAsync` + the pending-command correlation (`_pendingCommands`, `_sendLock`, `HandleCallResultAsync`, `HandleCallErrorAsync`) and `TriggerMessageAsync` (used to trigger BootNotification on connect).
- `Services/ChargerConfigurationService` — **on-connect config push** (see §7.1). Make its pushed values come from `ChargerSettings` instead of hard-coded.

**Kept outbound commands** (small `IChargerControl` facade for the API, replaces the old `OcppChargerGateway`):
- `ChangeAvailability` (`SetAvailabilityAsync` → connector Operative/Inoperative)
- `UnlockConnector`
- `ChangeConfiguration`, `GetConfiguration` (used by the config service)
- `TriggerMessage` (on-connect BootNotification trigger only)

**Inbound models / primitives:** `BootNotification`, `StatusNotification`, `Heartbeat`, `Authorize`, `StartTransaction`, `StopTransaction`, `MeterValues`, `DataTransfer` (accept+ack), `CommonTypes`, `OcppMessage`, `OcppMessageType`, `ChargerStatus`, `OcppCommandResult`, plus kept-command models `ChangeAvailability`, `ChangeConfiguration`, `GetConfiguration`, `TriggerMessage`, `UnlockConnector`.

### `Backend.OCPP` — STRIP (unused outbound + dead messages)
- `Services/OcppChargerGateway` (replaced by the slim `IChargerControl`).
- Outbound/unused message models: `RemoteStartTransaction`, `RemoteStopTransaction`, `Reset`, `ClearCache`, `ClearChargingProfile`, `SetChargingProfile`, `GetCompositeSchedule`, `GetLocalListVersion`, `SendLocalList`, `ReserveNow`, `CancelReservation`, `GetDiagnostics`, `DiagnosticsStatusNotification`, `FirmwareStatusNotification`, `UpdateFirmware`.
- In `ChargePointSession`: remove `RemoteStartTransactionAsync`, `RemoteStopTransactionAsync`, and the `DiagnosticsStatusNotification` / `FirmwareStatusNotification` inbound handlers (ack-only, unused). Keep `SetAvailabilityAsync` and `ChangeConfigurationAsync`.

### `Backend.OCPP` — AUTO-ACCEPT (keep as-is, make explicit)
`ChargePointSession` already returns `Accepted` for `BootNotification`, `Authorize`, `StartTransaction`, `StopTransaction`. Keep this — every transaction is accepted automatically with no id-tag whitelist. `BootNotification` response `Interval` (heartbeat seconds) becomes a `ChargerSettings` value.

### `Backend.OCPP` — DECOUPLE from the deleted layers
`ChargePointSession` currently depends on the deleted layers: `IDomainEventDispatcher`, `Domain.Events.*`, and four `Application` command handlers (`RegisterChargerHandler`, `BeginChargingSessionHandler`, `CompleteChargingSessionHandler`, `UpdateConnectorStatusHandler`). Rewrite its constructor to take a single `IChargerTelemetrySink` instead. On each inbound message it (1) returns the auto-accept response and (2) forwards the data to the sink; the sink owns both the in-memory snapshot and DB persistence. `ChargerStatusTracker` currently implements `IDomainEventHandler<…>` and references `Application.*` + `Domain.Events.*`. Replace with the same **telemetry sink** that the session calls directly:

```csharp
public interface IChargerTelemetrySink   // in Backend.OCPP
{
    void OnConnected(string chargePointId);
    void OnDisconnected(string chargePointId);
    void OnBoot(string chargePointId, ChargerInfo info);
    void OnConnectorStatus(string chargePointId, int connectorId, string status, string? errorCode);
    void OnTransactionStarted(string chargePointId, int connectorId, int transactionId, int meterStartWh, DateTimeOffset at);
    void OnTransactionStopped(string chargePointId, int transactionId, int meterStopWh, string? reason, DateTimeOffset at);
    void OnMeterValues(string chargePointId, MeterValuesRequest values);
}
```
`ChargerStatusTracker` keeps its in-memory snapshot role and also implements (or forwards to) this sink. The cost service subscribes to transaction/meter events.

### `Backend.HomeAssistant` — KEEP + EXTEND
- Keep: `Auth/*`, `BackgroundServices/TokenRefreshService` + `AuthStateCleanupService`, `Configuration/HomeAssistantAuthOptions`, `Models/*`, `HomeAssistantApiService`, `HomeAssistantConnectionManager`.
- **Delete**: `IHomeAutomationGateway`, `HomeAssistantGateway`, `EventHandlers/HomeAutomationEventHandler` (we no longer push state to HA).
- **Add** an actuator/reader:
  ```csharp
  public interface IHomeAssistantControl
  {
      Task<double?> GetBatterySocAsync(string entityId, CancellationToken ct);     // GET /api/states/{entityId}
      Task<string?> GetStateAsync(string entityId, CancellationToken ct);
      Task CallServiceAsync(string domain, string service, object? data, CancellationToken ct); // POST /api/services/{domain}/{service}
      Task<IReadOnlyList<HaEntity>> GetEntitiesAsync(CancellationToken ct);         // for the settings entity picker
  }
  ```
  Start/stop become generic service calls (`domain`, `service`, optional JSON `data`) so any actuator works: `button.press`, `switch.turn_on/off`, `number.set_value`, `script.turn_on`, etc.

### `Domain` → rename to `Core`, gut it
- Delete all `Events/*` and the event-sourcing bits on entities.
- Replace with plain models + pure calculators (Section 6). No EF attributes, no infra deps.

### DELETE `HASmartCharge.Application` (whole project)
- Remove from `.slnx` and from every `.csproj` ProjectReference.
- The one genuinely useful thing — the read-model snapshots — moves to DTOs in `Backend` or a small `Core` contract as needed.

### `Backend.DB` — replace the data model (Section 5).

### `Backend` — new controllers + background services (Sections 7–8). Remove `ChargersController`, `ChargerCommandsController`, `DashboardController`, and command request models. Keep `OcppController` (websocket) and `HomeAssistantAuthController`.

### `Frontend` — rewrite pages (Section 9). Keep the stack and the TanStack-Query + `api/` pattern.

---

## 5. Data model (EF Core / SQLite)

New `ApplicationDbContext` sets. Settings tables are single-row (seeded with `Id = 1`).

```csharp
PriceProviderSettings        // single row
  Id (1)
  ApiUrl            string   // default: https://epexprijzen.nl/api/v1/prices/nextenergy/hourly
  SupplierSlug      string   // "nextenergy" — UI edits this; ApiUrl templated from it
  Currency          string   // "EUR"
  RefreshMinutes    int      // default 60; also force-refresh after 13:00 for tomorrow

CarSettings                  // single row
  Id (1)
  Name              string
  BatteryCapacityKwh double
  TargetSocPercent  int      // default 100
  ChargeEfficiency  double   // default 0.90 (grid→battery loss, for energy math)
  HaSocEntityId         string   // e.g. sensor.car_battery_level
  HaStartDomain/Service/DataJson    // service call to start charging
  HaStopDomain/Service/DataJson     // service call to stop charging
  HaPluggedInEntityId   string?  // optional binary_sensor
  HaChargingStateEntityId string? // optional, cross-check actual charging
  HaTargetSocEntityId   string?  // optional, push target to car

ChargerSettings              // single row (schema allows >1)
  Id (1)
  ChargePointId     string   // must match the OCPP ws path id
  FriendlyName      string
  MaxChargeKw       double
  ConnectorId       int      // default 1
  // --- pushed to the charger on connect (ChangeConfiguration) ---
  HeartbeatInterval         int    // default 60; sent as BootNotification response Interval
  MeterValueSampleInterval  int    // default 10 (s) — drives cost granularity
  ClockAlignedDataInterval  int    // default 10 (s)
  MeterValuesSampledData    string // CSV measurands; default includes Energy.Active.Import.Register,Power.Active.Import,...

HourlyPrice                  // price cache + history
  HourStartUtc      DateTime  (PK)
  PricePerKwh       decimal
  FetchedAt         DateTime

ChargePlan
  Id
  DeadlineUtc       DateTime
  TargetSocPercent  int
  StartSocPercent   int?      // captured at creation
  Status            enum { Pending, Active, Completed, Cancelled, MissedDeadline }
  EstimatedEnergyKwh double
  EstimatedCost     decimal
  SelectedHoursJson string    // recomputed each tick; UTC hour starts
  CreatedAt / CompletedAt

ChargeSession                // one per OCPP transaction; telemetry + cost
  TransactionId     int (PK, from OCPP)
  ChargePointId / ConnectorId
  StartedAt / CompletedAt
  MeterStartWh / MeterStopWh
  TotalKwh          double
  TotalCost         decimal
  PlanId            int?  (FK, nullable)

HourlyEnergyUsage            // per-session per-hour breakdown → cost
  Id
  SessionId         int (FK)
  HourStartUtc      DateTime
  EnergyKwh         double
  PricePerKwh       decimal
  Cost              decimal
```

**Migration strategy — full reset (user-authorized, no data to preserve):**
1. Delete `hasmartcharge.db` (repo root; conn string `Data Source=hasmartcharge.db`).
2. Delete the entire `HASmartCharge.Backend.DB/Migrations/` folder — both existing migrations (`20260213133922_Initial migration`, `20260307150813_AddChargersConnectorsTransactions`) and `ApplicationDbContextModelSnapshot.cs`.
3. Delete the old EF models (`Charger`, `Connector`, `ChargingTransaction`) and repositories; rewrite `ApplicationDbContext` with the new sets (§5).
4. Generate one fresh `InitialSchema` migration. Startup `MigrateAsync` recreates the DB.

Keep the `HomeAssistantConnection` model/table — the OAuth2 layer still uses it (it becomes part of the fresh `InitialSchema`).

---

## 6. Core logic (pure, unit-testable — `HASmartCharge.Core`)

### 6.1 Schedule calculator

```
Inputs: currentSoc%, targetSoc%, capacityKwh, efficiency, maxChargeKw,
        nowUtc, deadlineUtc, IReadOnlyList<HourlyPrice>

energyNeededKwh = max(0, (targetSoc - currentSoc) / 100 * capacityKwh) / efficiency
if energyNeededKwh == 0:        -> { Done = true, hours = [] }
hoursNeeded     = ceil(energyNeededKwh / maxChargeKw)

candidates = prices
   .where(h => h.HourStartUtc + 1h > nowUtc && h.HourStartUtc < deadlineUtc)
   .orderBy(h => h.PricePerKwh)

selected = candidates.Take(hoursNeeded)         // cheapest N hours
feasible = candidates.Count >= hoursNeeded       // false => deadline too tight / prices missing

estimatedCost = Σ over selected of (kWhInThatHour * price)
   where kWhInThatHour = maxChargeKw for full hours,
                         remainder for the final partial hour
```

Returns: `{ Feasible, EnergyNeededKwh, HoursNeeded, SelectedHourStartsUtc[], EstimatedCost }`.

**Documented edge cases:**
- Deadline in the past or before the next price hour → `Feasible = false`, empty selection (UI warns).
- `tomorrow` prices not yet published → schedule only within the known horizon; the orchestrator re-runs the calc when new prices arrive.
- SoC already ≥ target → `Done`.
- Not enough cheap hours before deadline → select all available, flag `Feasible = false` (UI: "can't make the deadline at max kW").

### 6.2 Cost attributor

```
Inputs: ordered meter samples [(timestampUtc, cumulativeKwh)], hourly prices

for each consecutive pair (a, b):
    deltaKwh = b.kwh - a.kwh
    split deltaKwh across the clock-hours the interval [a.ts, b.ts] spans,
        proportional to time in each hour
    for each hour bucket: cost += kwhInHour * price(hour)

=> HourlyEnergyUsage rows + TotalKwh + TotalCost
```

Fallback when only `meterStart`/`meterStop` exist (no periodic MeterValues): single bucket using the average price across the session window (documented as lower fidelity).

---

## 7. Background services + OCPP wiring (`Backend`)

### 7.1 On-connect charger config push (`ChargerConfigurationService`)
When a charger connects, the session (after a short settle delay) triggers a `BootNotification`, then pushes config via `ChangeConfiguration` so the charger reports meter data the way we need:
- `MeterValueSampleInterval` = `ChargerSettings.MeterValueSampleInterval`
- `ClockAlignedDataInterval` = `ChargerSettings.ClockAlignedDataInterval`
- `MeterValuesSampledData` / `MeterValuesAlignedData` = `ChargerSettings.MeterValuesSampledData`

These were hard-coded; move them to `ChargerSettings`. `BootNotification` response `Interval` (heartbeat) = `ChargerSettings.HeartbeatInterval`. A `GetConfiguration` is sent first to log what the charger supports. Finer meter sample interval → better per-hour cost attribution (§6.2).

### `PriceFetchService` (IHostedService)
- On startup + every `RefreshMinutes`, and a forced run shortly after 13:00 CET to grab `tomorrow`.
- `GET {ApiUrl}` with a browser `User-Agent` (required — see §2). Parse `today` + `tomorrow`, upsert `HourlyPrice`.
- Tolerate empty `tomorrow`; log and retry next cycle.

### `ChargeOrchestratorService` (IHostedService, ticks every ~60s)
```
plan = active ChargePlan; if none -> idle
soc  = HA.GetBatterySocAsync(car.HaSocEntityId); if null -> log + skip tick
if soc >= plan.TargetSoc -> HA stop; plan.Status = Completed; return
result = ScheduleCalculator(soc, target, capacity, eff, maxKw, now, deadline, prices)
persist result.SelectedHours + EstimatedCost on the plan
nowHour      = floor(now → hour)
shouldCharge = result.SelectedHours.Contains(nowHour)
isCharging   = OCPP connector status == "Charging"  (cross-check HaChargingStateEntityId if set)
if shouldCharge && !isCharging -> HA start
if !shouldCharge && isCharging -> HA stop
if now > deadline && soc < target -> Status = MissedDeadline (policy: keep charging until full, but flag it)
```
- Idempotent: only calls HA start/stop on a state transition (avoid spamming services every tick).
- Manual override (Section 8) pauses the plan's automatic toggling for a configurable window.

### Cost attribution
- Subscribe to the OCPP telemetry sink. On `OnMeterValues` accumulate samples; on `OnTransactionStopped` finalize the `ChargeSession` via the cost attributor and persist `HourlyEnergyUsage`.

---

## 8. API surface (`Backend` controllers)

```
# Settings
GET  /api/settings/price        PUT /api/settings/price
GET  /api/settings/car          PUT /api/settings/car
GET  /api/settings/charger      PUT /api/settings/charger

# Home Assistant
(existing OAuth2 connect/callback endpoints — kept)
GET  /api/ha/status                       # connected? token valid?
GET  /api/ha/entities?domain=sensor       # entity picker for settings UI

# Prices
GET  /api/prices?from=&to=                # cached hourly prices for the graph
POST /api/prices/refresh                  # force a fetch

# Charger
GET  /api/charger/status                  # connected, connector status, live kW, session kWh
POST /api/charger/unlock                  # OCPP UnlockConnector
POST /api/charger/availability { available }   # OCPP ChangeAvailability (Operative/Inoperative)
POST /api/charger/reconfigure             # re-push ChargerSettings config to the charger

# Plan
GET    /api/plan                          # current/active plan + computed selection
POST   /api/plan      { deadlineUtc, targetSocPercent }
GET    /api/plan/preview?deadline=&targetSoc=   # compute selection+cost WITHOUT saving
DELETE /api/plan                          # cancel active plan

# Manual control + history
POST /api/charge/start                    # HA start (override)
POST /api/charge/stop                     # HA stop (override)
GET  /api/sessions                        # history: kWh + cost per session
GET  /api/sessions/{transactionId}        # detail + hourly breakdown
```

---

## 9. Frontend rewrite (`Frontend`)

Keep React 19 + Vite + TanStack Query + recharts + Tailwind. Replace routes/pages.

**Routes:** `/` Dashboard, `/schedule` Schedule, `/settings` Settings, `/history` History.

- **Dashboard**
  - Current battery % (gauge), charging state, charger connection status, live kW + session kWh/cost.
  - Active plan summary: target %, deadline, est. finish, est. cost, feasible/warning badge.
  - **Today+tomorrow price graph** (recharts bar/area, €/kWh) with the **selected charge hours highlighted** and "now" marker.
- **Schedule**
  - Pick deadline (date+time) and target %. Live `preview` call → shows selected hours on the graph, energy needed, estimated cost, feasibility warning. "Create plan" / "Cancel plan".
- **Settings** (tabs)
  - *Price provider*: supplier slug / URL, currency, refresh interval. "Test fetch" button.
  - *Car*: name, capacity kWh, target %, efficiency; HA entity pickers for SoC sensor; start service (domain/service/data); stop service; optional plugged-in / charging-state / target-SoC entities.
  - *Charger*: charge point ID, friendly name, max charge kW, connector ID; on-connect config (heartbeat interval, meter sample/clock-aligned intervals, sampled measurands CSV). Buttons: unlock connector, set available/inoperative, re-push config.
  - *Home Assistant*: OAuth2 connect/disconnect (existing flow) + connection status.
- **History**: table of past sessions (start, duration, kWh, € cost, avg €/kWh), expandable hourly breakdown.

New `src/api/*` modules + `src/types/*` to match the API above. Drop the chargers/connector/command components and the old types.

---

## 10. Phased execution

| Phase | Work | Done when |
|---|---|---|
| **0. Branch** | Already on `feature/project-architecture-rewrite`. Commit this plan. | plan.md committed |
| **1. Demolition** ✅ | Delete `Application` project; strip unused OCPP commands/messages (keep config-push + unlock + availability + correlation infra); delete HA publish gateway; rewrite `ChargePointSession` + `ChargerStatusTracker` onto `IChargerTelemetrySink`; gut `Domain`→`Core`; fix `Program.cs` DI + `.slnx`. | **DONE 2026-06-05.** Solution builds (0 errors), boots, DI resolves, fresh `InitialSchema` migration applies cleanly. No CQRS. See `plan-report.md`. |
| **2. Data** | New EF entities + single fresh migration; settings seeding. | DB creates clean; settings CRUD round-trips. |
| **3. HA control** | `IHomeAssistantControl` (read SoC, call services, list entities) + `/api/ha/entities`, `/api/settings/*`. | Can read SoC and fire a test service call from the API. |
| **4. Prices** | `PriceFetchService` (browser UA, today+tomorrow), `HourlyPrice` cache, `/api/prices`. | Prices populate + graph data returns. |
| **5. Scheduling** | `Core` ScheduleCalculator + unit tests; `/api/plan` + `/api/plan/preview`. | Preview returns correct cheapest-hour selection + cost. |
| **6. Orchestrator** | `ChargeOrchestratorService` control loop (idempotent HA start/stop) + manual override endpoints. | Toggles HA start/stop on hour boundaries against a live/simulated car. |
| **7. Cost** | Telemetry sink → cost attributor → `ChargeSession`/`HourlyEnergyUsage`; `/api/sessions`. | A completed session shows kWh + € cost bucketed by hour. |
| **8. Frontend** | Rewrite Dashboard, Schedule, Settings, History; price graph with highlighted hours. | Full flow usable in the UI. |
| **9. E2E + polish** | OCPP simulator + HA against the loop; deadline/feasibility edge cases; README + `OCPP_README` update. | End-to-end: set deadline → cheapest-hour charge → full by deadline → cost shown. |

---

## 11. Risks & assumptions

- **HA actuator variety** — start/stop entity might be a button, switch, number, or script. Mitigated by generic `domain`/`service`/`data` service calls instead of hard-coding a button press.
- **Day-ahead prices** — `tomorrow` is empty until ~13:00 CET. A deadline needing tomorrow's cheap hours can't be fully planned until then; orchestrator re-plans when prices land. Surface this in the UI.
- **Cost fidelity** — if the charger doesn't send periodic `MeterValues`, per-hour cost falls back to session-average price. Charger config (meter sample interval) affects accuracy.
- **Efficiency / partial hours** — charging curve isn't linear near 100%; `ChargeEfficiency` + integer-hour rounding are approximations. Re-evaluation each tick + stop-on-target-SoC corrects drift.
- **Single car / single charger** — schema permits more, UI/orchestrator assume one of each (per the requirement "let's not overcomplicate").
- **OCPP is advisory** — since we never command the charger, "is it charging" is inferred from `StatusNotification`; optionally cross-checked against a HA charging-state entity.
- **Migration reset** — dev-only; the SQLite DB is wiped for the fresh schema.

## 12. Out of scope (v1)

OCPP charge-current limiting / smart-charging profiles; multi-car / multi-charger UX; solar/PV surplus; dynamic re-pricing mid-hour; user accounts/multi-tenant; mobile app.
