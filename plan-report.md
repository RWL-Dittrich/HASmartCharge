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
