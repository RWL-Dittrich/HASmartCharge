# Rewrite Progress Report

Phase status (plan.md §10): **1–8 ✅ · 9 ⏳**

---

# Phase 8 — Frontend rewrite: Report

**Date:** 2026-07-12
**Method:** Sonnet subagent, verified by orchestrating session (`npm run build` exit 0, dev-proxy smoke against live backend).

- **Deleted:** chargers/analytics-era code (chargersApi, useChargers/useDashboard/useChargerCommands, components/chargers/*, ChargersPage, AnalyticsPage, old types/charger).
- **New API layer:** src/api/client.ts (shared apiFetch/ApiError) + per-domain modules (settings, ha, prices, plan, charger, charge, sessions) + matching hooks (charger/plan 10s refetch, HA 30s, prices 5min) + src/types/* mirroring the backend contract.
- **Pages:** Dashboard (SoC, charger card, HA badge, active-plan card, PriceChart with selected hours highlighted + now marker, manual start/stop), Schedule (deadline + target pickers, debounced live preview, create/cancel), Settings tabs (price provider w/ refresh-now, car w/ HA entity pickers + service-call JSON validation, charger w/ unlock/availability/re-push, HA OAuth connect/disconnect), History (sessions table + expandable hourly cost breakdown). Layout shell (AppLayout/Sidebar/TopBar) kept, nav updated.
- **Cross-cutting fix (backend):** `/api/prices` and `/api/plan/preview` emitted dates without the `Z` suffix (SQLite Kind=Unspecified). Fixed at the source (UTC restamp in PricesController, `EnsureUtc` in Preview) *and* defensively in the frontend (`ensureUtcSuffix`).
- **Known rough edges:** single ~687 KB JS bundle (no code-splitting yet); sidebar footer/logo placeholder text unchanged; HA OAuth full-page redirect flow untested without a real HA instance.

---

# Phases 3–7 — Backend feature build-out: Report

**Date:** 2026-07-12
**Branch:** `feature/project-architecture-rewrite`
**Method:** Implemented by Sonnet subagents (one per phase, sequenced), each spec'd from plan.md and independently verified by the orchestrating session (build, unit tests, live curl smoke tests against a running backend).

**Result:** ✅ Backend feature-complete per plan §§6–8. Build 0 errors; 21/21 Core unit tests pass; every endpoint smoke-tested live.

## Phase 3 — HA control ✅
- `IHomeAssistantControl` + `HomeAssistantControl` (Backend.HomeAssistant): `GetBatterySocAsync` (null on unavailable/unknown/disconnected, invariant parse), `GetStateAsync`, `CallServiceAsync(domain, service, dataJson)` (throws InvalidOperationException when HA disconnected), `GetEntitiesAsync` → `HaEntitySummary(EntityId, FriendlyName, State)`. Mirrors HomeAssistantApiService's HTTP/auth pattern (per-call client, Bearer token from connection manager).
- `HomeAssistantController` (`api/ha`): `GET status` `{connected, baseUrl, tokenExpiresAt}`, `GET entities?domain=` (prefix filter).
- Verified: 200s with graceful disconnected responses.

## Phase 4 — Prices ✅
- `IPriceFetcher`/`EpexPriceFetcher` (Backend/Services, scoped): reads PriceProviderSettings.ApiUrl, browser UA (provider 403s without), parses `{today, tomorrow}` arrays, batch upsert into HourlyPrices keyed on UTC hour, single SaveChanges, never throws → `PriceFetchResult(Success, PricesUpserted, TomorrowAvailable, Error)`.
- `PriceFetchService` (BackgroundService): fetch at startup (+5s), loop on RefreshMinutes (5-min floor, re-read each tick), plus a 13:05 Europe/Amsterdam wake-up when tomorrow's prices are missing (IANA tz id with Windows fallback).
- `PricesController` (`api/prices`): `GET ?from=&to=` (default window today UTC → +48h), `POST refresh`.
- Verified live against the real EPEX API: 24 rows upserted, correct UTC hour keys; tomorrow empty pre-13:00 CET as expected.

## Phase 5 — Scheduling ✅
- `HASmartCharge.Core\Scheduling\`: `ScheduleCalculator.Calculate(ScheduleRequest) → ScheduleResult` — pure, plan §6.1 algorithm. Cheapest-N-hours selection (price asc, hour asc tie-break), grid-side energy incl. efficiency, remainder energy costed on the **most expensive** selected hour, guards (efficiency≤0→1.0, MaxChargeKw≤0→infeasible, past deadline, empty prices), infeasible → selects all candidates + Feasible=false.
- `HASmartCharge.Core.Tests` (new xunit project, added to slnx): 13 calculator tests.
- `PlanController` (`api/plan`) + `IPlanScheduleService` helper: `GET` (active plan, 404 if none), `GET preview?deadline=&targetSoc=` (no save; SoC-unavailable → 200 + warning, worst-case SoC 0), `POST` (cancels existing active, Status=Active, persists selection JSON), `DELETE` (cancel, 204).
- Subagent found + fixed a real bug: SQLite round-trips DateTime as Kind=Unspecified → `EnsureUtc` applied in DTO mapping and on inbound deadlines, so JSON consistently carries the `Z` suffix.
- Verified live: full lifecycle preview→create→get→delete, feasible + infeasible + missing-deadline (400) cases.

## Phase 6 — Orchestrator ✅
- `ChargeOrchestratorService` (BackgroundService, 60s tick, scope per tick, tick-level catch-all): active plan (Pending auto-promoted; MissedDeadline still controlled), manual-override skip, SoC gate (skip tick when unavailable), target-reached → HA stop + Completed, per-tick schedule recompute persisted onto the plan, `shouldCharge = selected hours ∋ current UTC hour` vs `isCharging` (OCPP connector status "Charging", OR'd with optional HA charging-state entity) — HA start/stop called **only on transitions**; past-deadline → MissedDeadline flag but keeps charging toward target (plan §7 policy).
- `IChargeControlService`/`ChargeControlService`: HA start/stop service calls from CarSettings with clear config-error messages. `ManualOverrideState` (singleton, thread-safe until-timestamp).
- `ChargeController` (`api/charge`): `POST start|stop?overrideMinutes=60` → manual HA call + override window (503 when HA unconfigured/disconnected).
- `ChargerController` (`api/charger`): `GET status` (tracker snapshot), `POST unlock|availability|reconfigure` (IChargerControl; 404 when ChargePointId unset).
- Verified live: idle tick clean, correct 503/404 envelopes with no HA/charger attached.

## Phase 7 — Cost attribution ✅
- `HASmartCharge.Core\Costing\CostAttributor.Attribute(samples, prices)` — pure, plan §6.2: consecutive-sample deltas (clamped ≥0) split across UTC clock-hours proportional to time, per-hour buckets × price, missing price → 0-cost bucket still emitted, defensive sort, <2 samples → empty. 8 new tests (21 total).
- `ChargeSessionRecorder` (Backend, singleton `IChargerTelemetrySink`): TransactionStarted → ChargeSession row (+PlanId of active plan, transaction-id-reuse overwrite); MeterValues → in-memory `Energy.Active.Import.Register` samples per transaction; TransactionStopped → CostAttributor → totals + HourlyEnergyUsage rows persisted. Scope-per-event, never throws into the OCPP session; restart mid-session falls back to start/stop-only attribution.
- `TelemetryFanout`: `IChargerTelemetrySink` now fans out to ChargerStatusTracker + ChargeSessionRecorder (per-sink try/catch).
- `SessionsController` (`api/sessions`): list (newest first, avg €/kWh) + detail with hourly breakdown, UTC-kind-corrected dates.
- **Orchestrator-session fix during review:** both ChargeSessionRecorder and ChargerStatusTracker treated a *missing* MeterValues unit as kWh; OCPP 1.6 defaults energy measurands to **Wh**. Missing unit now normalized as Wh→kWh in both — prevents 1000× cost/energy errors with chargers that omit the unit field.

## Deferred
- Settings validation, README/OCPP_README refresh, E2E with an OCPP simulator (phase 9).

---

# Phase 2 — Data: Report

**Date:** 2026-07-12
**Branch:** `feature/project-architecture-rewrite`
**Goal:** New EF data model (settings, prices, plans, sessions), single-row settings seeding, settings CRUD, and the deferred phase-1 item: on-connect config push values from `ChargerSettings` instead of hard-coded.

**Result:** ✅ Solution builds (0 errors, 1 pre-existing warning), backend boots, `ChargingDomain` migration applies cleanly, all three settings endpoints round-trip GET→PUT→GET.

## 1. New entities (`Backend.DB/Models`)

Per plan §5, all seeded/keyed as specified:

| Entity | Key | Notes |
|---|---|---|
| `PriceProviderSettings` | Id=1 seeded | Defaults: nextenergy URL, EUR, 60 min refresh |
| `CarSettings` | Id=1 seeded | Capacity/target/efficiency + HA entity ids + generic start/stop service calls (domain/service/dataJson) |
| `ChargerSettings` | Id=1 seeded | ChargePointId, MaxChargeKw (scheduling only) + on-connect config values (heartbeat, meter sample/clock-aligned intervals, measurands CSV) |
| `HourlyPrice` | PK `HourStartUtc` | decimal €/kWh + `FetchedAt` |
| `ChargePlan` | Id | `ChargePlanStatus` enum (Pending/Active/Completed/Cancelled/MissedDeadline), `SelectedHoursJson` |
| `ChargeSession` | PK `TransactionId` (`ValueGeneratedNever`, from OCPP) | FK `PlanId` (SetNull), cascade-owns hourly usage |
| `HourlyEnergyUsage` | Id | FK `SessionId`, per-hour kWh/price/cost |

`ApplicationDbContext`: 7 new `DbSet`s; single-row seeding via `HasData` (Id=1) for the three settings tables. `HomeAssistantConnection` untouched.

## 2. Migration

- Additive **`20260712105754_ChargingDomain`** (not a reset — preserves `HomeAssistantConnections` OAuth tokens). 7 `CreateTable` + 3 seed `InsertData`.
- Applied via the existing unconditional `MigrateAsync()` at startup.

## 3. Config push from settings (deferred phase-1 item)

- New **`IOcppChargerConfigurationProvider`** + `OcppChargerConfiguration` record in `Backend.OCPP` — the seam that keeps Backend.OCPP free of DB references.
- **`DbOcppChargerConfigurationProvider`** in `Backend/Services` implements it against `ChargerSettings` (scope-per-call, falls back to `OcppChargerConfiguration.Default` when the row is missing or the DB errors).
- `ChargerConfigurationService`: hard-coded interval/measurand values replaced by provider values; dead `ConfigureChargerMinimalAsync` removed.
- `ChargePointSession`: `BootNotification` response `Interval` now comes from `ChargerSettings.HeartbeatInterval` (handler made async); ctor + `OcppConnectionOrchestrator` take the provider.

## 4. API

- New **`SettingsController`**: `GET`/`PUT` for `/api/settings/price`, `/api/settings/car`, `/api/settings/charger`. PUT updates the single seeded row and ignores incoming Id.

## 5. Verification

- `dotnet build` → 0 errors, 1 pre-existing warning.
- Backend boots, migration applies, seeds present.
- Round-trip verified live via curl on all three endpoints: GET returns seeded defaults → PUT custom values → GET returns them. Test values reset to defaults afterwards.

## 6. Not done in Phase 2 (deferred per plan)

- No price fetching (phase 4), no plan/session writes (phases 5–7) — tables exist, nothing populates them yet.
- Settings have no validation beyond model binding (e.g. efficiency 0..1, positive kW) — tighten when the Settings UI lands (phase 8).

---

# Phase 1 — Demolition: Report

**Date:** 2026-06-05
**Branch:** `feature/project-architecture-rewrite`
**Goal:** Strip the CQRS/DDD scaffolding and over-built OCPP surface down to a buildable core that boots, auto-accepts charger transactions, and pushes config on connect. No new feature work.

**Result:** ✅ Solution builds (0 errors, 1 pre-existing warning), backend boots, DI fully resolves, DB resets to a fresh `InitialSchema` migration that applies cleanly.

---

## 1. Projects

| Project | Action |
|---|---|
| `HASmartCharge.Application` | **Deleted** (entire CQRS layer: commands, handlers, events, dispatcher, interfaces, query snapshots). Removed from `.slnx` + all `ProjectReference`s. |
| `HASmartCharge.Domain` → `HASmartCharge.Core` | **Renamed** via `git mv` (history preserved). Gutted: deleted `Entities/` + `Events/`. Left `AssemblyReference.cs` as a placeholder (namespace → `HASmartCharge.Core`) for the pure models/calculators arriving in phases 2/5. |
| `HASmartCharge.Backend.OCPP` | Rewired to be self-contained (dropped refs to Application + Domain). |
| `HASmartCharge.Backend.DB` | Dropped Application ref; `ApplicationDbContext` reduced to `HomeAssistantConnections` only. |
| `HASmartCharge.Backend.HomeAssistant` | Dropped Application + Domain refs (kept Backend.DB). |
| `HASmartCharge.Backend` | Dropped Application ref; added Core ref; rewrote `Program.cs`. |
| `HASmartCharge.AppHost` | Untouched. |

## 2. OCPP — kept vs stripped

**Kept (transport + passive listener + minimal outbound):**
- `Transport/`, `Domain/SessionManager`, `Application/OcppMessageRouter`, `Infrastructure/OcppConnectionOrchestrator`, `Services/WebSocketMessageService`.
- Outbound send + call-result correlation in `ChargePointSession` (`SendCommandAsync`, `_pendingCommands`, `_sendLock`) — required by the config push.
- `Services/ICommandSender` + `SessionCommandSender`, `Services/ChargerConfigurationService`.
- Kept message models: Boot/Heartbeat/Authorize/Start/Stop/Status/MeterValues/DataTransfer + `ChangeAvailability`, `ChangeConfiguration`, `GetConfiguration`, `TriggerMessage`, `UnlockConnector`.

**Stripped:**
- `Services/OcppChargerGateway.cs` (old `IChargerGateway` impl).
- 15 dead message models: `RemoteStartTransaction`, `RemoteStopTransaction`, `Reset`, `ClearCache`, `ClearChargingProfile`, `SetChargingProfile`, `GetCompositeSchedule`, `GetLocalListVersion`, `SendLocalList`, `ReserveNow`, `CancelReservation`, `GetDiagnostics`, `DiagnosticsStatusNotification`, `FirmwareStatusNotification`, `UpdateFirmware`.
- `ChargePointSession`: removed `RemoteStartTransactionAsync` / `RemoteStopTransactionAsync` and the Diagnostics/Firmware inbound handlers. `IChargePointSession` trimmed to match.

## 3. New / rewritten files

- **`Services/IChargerTelemetrySink.cs`** (new) — the seam that replaces the domain-event + CQRS pipeline. The session calls it as inbound messages arrive: `OnConnected/OnDisconnected/OnBoot/OnConnectorStatus/OnTransactionStarted/OnTransactionStopped/OnMeterValues`.
- **`Services/ChargerStatusTracker.cs`** (rewritten) — now implements `IChargerTelemetrySink` instead of `IChargerReadModel` + 6 `IDomainEventHandler<>`. Keeps the in-memory snapshot (`ChargerStatus`/`ConnectorStatus`/`ConnectorMeasurands`) and the `Energy.Active.Import.Register` Wh→kWh measurand parsing (the future cost-calc data source). Dropped the Application-typed snapshot mappers + `SeedFromDomainChargers`.
- **`Domain/ChargePointSession.cs`** (rewritten) — ctor now takes `(chargePointId, connection, logger, ChargerConfigurationService, IChargerTelemetrySink)`; dropped the dispatcher + 4 Application command handlers. All inbound transactions **auto-accepted** (`BootNotification`/`Authorize`/`StartTransaction`/`StopTransaction` → `Accepted`, no id-tag whitelist). Transaction IDs generated locally (in-memory counter seeded from unix-seconds). Kept the on-connect `TriggerMessage(BootNotification)` → `ConfigureChargerAsync` push.
- **`Services/ChargerControl.cs`** (new) — slim `IChargerControl` facade (replaces `OcppChargerGateway`): `SetConnectorAvailabilityAsync`, `UnlockConnectorAsync`, `ReconfigureAsync`. Backed by `ICommandSender` + `ChargerConfigurationService`. No endpoint wired yet (controllers come in later phases).
- **`Infrastructure/OcppConnectionOrchestrator.cs`** — ctor dropped dispatcher + 4 handlers, takes `IChargerTelemetrySink`; passes it to the session.
- **`Backend/Program.cs`** (rewritten) — removed all CQRS/event/repo/gateway registrations + the event-wiring and charger-seeding startup blocks. Now registers: HA auth stack (unchanged, OAuth2 kept), OCPP transport/router/session/orchestrator, `ChargerStatusTracker` as `IChargerTelemetrySink`, `ICommandSender`, `ChargerConfigurationService`, `IChargerControl`.

## 4. Database reset (user-authorized)

- Deleted `hasmartcharge.db` and the entire old `Migrations/` folder (both old migrations + snapshot).
- Deleted old EF models (`Charger`/`Connector`/`ChargingTransaction`) + `EfChargerRepository`/`EfChargingSessionRepository`.
- `ApplicationDbContext` reduced to `HomeAssistantConnections` (OAuth2 still needs it).
- Generated one fresh migration **`20260605205350_InitialSchema`** (creates `HomeAssistantConnections` only). The charging-domain tables land in phase 2.

## 5. Controllers

- Deleted `ChargersController`, `ChargerCommandsController`, `DashboardController` (CQRS-era, depended on the deleted read model / gateway) and their `Models/Charger/` request DTOs.
- Kept `OcppController` (WebSocket endpoint) and `HomeAssistantAuthController`.
- Net effect: the only live HTTP endpoints right now are the OCPP WebSocket + HA OAuth. New REST surface arrives in later phases — the frontend (phase 8) is expected to be broken until then.

## 6. Verification

- `dotnet build HASmartCharge.slnx` → **0 errors**, 1 warning (`CS8603` in `HomeAssistantApiService.cs:43`, pre-existing, not introduced here).
- `dotnet ef migrations add InitialSchema` → generated, contains only `HomeAssistantConnections`.
- Backend boots: `Now listening on http://0.0.0.0:5293`, `Application started`. All OCPP + HA + telemetry-sink + command-sender + control services constructed (DI graph valid).
- Fresh DB created and migrated (4 KB file, `InitializeAsync` reads the table with no error).

### Notable fix
`Program.cs` previously guarded migration with `if ((await GetPendingMigrationsAsync()).Any())`. On EF Core 10 that returns false when the SQLite file doesn't exist yet, so the table was never created → `no such table: HomeAssistantConnections`. Replaced with an unconditional, idempotent `await dbContext.Database.MigrateAsync();`.

## 7. Not done in Phase 1 (deferred per plan)

- Config-push values are still hard-coded in `ChargerConfigurationService` (move to `ChargerSettings` in phase 2).
- No DB persistence of telemetry/sessions yet — the sink is in-memory only (cost attribution = phase 7).
- `IChargerControl` has no HTTP endpoint yet (phase 8 / charger endpoints).
- Real charger connect not tested (no physical hardware available); verification covered build + DI + boot + migration.

## 8. Suggested commit

```
refactor: phase 1 demolition — drop CQRS, slim OCPP, reset DB

- delete HASmartCharge.Application (CQRS) + rename Domain -> Core (gutted)
- OCPP: passive telemetry via IChargerTelemetrySink, auto-accept txns,
  keep config-push/unlock/availability, strip 15 dead message models
- DB: reset to fresh InitialSchema (HomeAssistantConnections only)
- rewrite Program.cs DI; remove CQRS controllers; keep OCPP ws + HA OAuth
```
