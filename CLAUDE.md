# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

HASmartCharge charges **one EV as cheaply as possible by a deadline**: it picks the cheapest EPEX hourly electricity prices, toggles charging via **Home Assistant service calls** (never via OCPP commands), and uses the **OCPP 1.6J charger purely as a telemetry source** to measure delivered kWh and compute actual cost. Single car, single charger, no multi-tenancy.

`plan.md` is the authoritative architecture/requirements document (phases 1–8 done, phase 9 E2E pending); `plan-report.md` logs what was actually built per phase. Read those before structural changes. `README.md` and `OCPP_VERIFICATION.md` predate the rewrite and are partly stale (e.g. they claim the full OCPP command surface still exists — it doesn't).

## Commands

```powershell
dotnet build HASmartCharge.slnx                # full build (expect 0 errors / 0 warnings)
dotnet test HASmartCharge.Core.Tests           # unit tests (schedule + cost logic)
dotnet run --project HASmartCharge.Backend     # API on http://localhost:5293 (launchSettings)
npm run dev    # in HASmartCharge.Frontend — Vite on :5173, proxies /api to VITE_BACKEND_URL (default http://127.0.0.1:5000 — set VITE_BACKEND_URL=http://localhost:5293 when running backend standalone)
npm run build  # frontend typecheck + bundle
dotnet run --project HASmartCharge.AppHost     # Aspire: backend + frontend wired together (sets VITE_BACKEND_URL automatically)

# EF migrations (SQLite, DB file: hasmartcharge.db at repo root)
dotnet ef migrations add <Name> --project HASmartCharge.Backend.DB --startup-project HASmartCharge.Backend
```

- Startup runs `Database.MigrateAsync()` unconditionally — new migrations apply on boot.
- `dotnet run --no-build` right after `ef migrations add` can crash with `PendingModelChangesWarning` from stale binaries; rebuild first.
- **The user often has the backend running under the JetBrains Rider debugger.** Builds then fail with MSB3027/MSB3021 file-lock errors naming `JetBrains.Debugger.Worker` — that's not a code error; ask the user to stop the debug session rather than killing their process.
- On Windows, bash-style `kill` does not reliably stop `dotnet run` process trees — use PowerShell `Stop-Process -Force`.

## Architecture

```
Frontend ──HTTP /api──► Backend ──► Backend.DB (EF Core / SQLite)
                          │  ├────► Backend.HomeAssistant ──► HA REST (/api/states, /api/services)
                          │  └────► Backend.OCPP ◄──WS /ocpp/1.6/{chargePointId}── charger
                          └───────► Core (pure logic, zero dependencies)
```

- **HASmartCharge.Core** — pure, dependency-free logic. `Scheduling/ScheduleCalculator` (cheapest-N-hours selection before a deadline; remainder energy costed on the most expensive selected hour) and `Costing/CostAttributor` (splits metered kWh across UTC clock-hours proportional to time, prices each bucket). Both static + unit-tested in `HASmartCharge.Core.Tests`. Keep this project free of EF/ASP.NET/HTTP.
- **HASmartCharge.Backend.OCPP** — self-contained OCPP 1.6J central system (transport, session manager, message router). **Telemetry-first by design**: all inbound transactions are auto-accepted (no id-tag whitelist), and outbound commands are limited to UnlockConnector, ChangeAvailability, ChangeConfiguration/GetConfiguration, TriggerMessage (on-connect config push), and **SetChargingProfile** (a flat current cap in A, `ChargerControl.SetChargingCurrentLimitAsync`). Do NOT add RemoteStart/Stop — charging start/stop stays in Home Assistant (plan.md §1). SetChargingProfile only caps delivered current; it never starts or stops a transaction. The original "no charging profiles" lock was reversed 2026-07-12 to add the dashboard charge-power slider.
  - **Charge-power slider:** dashboard slider works in **kW** → `POST /api/charger/power {kw}` → clamps to `ChargerSettings.ChargePowerMin/MaxKw`, converts kW→amps with `A = W / (PhaseCount × SupplyVoltage)` (rounded down to 0.1 A so it never overshoots), sends a `TxDefaultProfile`/`Relative` SetChargingProfile in **amps** (unit `A`, `numberPhases = PhaseCount`), and persists `ChargePowerSetpointKw` only when the charger replies `Accepted`. The kW→A conversion is deliberate: most OCPP 1.6 chargers cap *current*, not power. Min/max/voltage/phases are edited on the charger settings tab; the setpoint is owned by the power endpoint (the settings PUT must not touch it). Reuses the `ChargingProfile`/`ChargingSchedule` types in `CommonTypes.cs`, whose optional fields are `[JsonIgnore(WhenWritingNull)]` so strict chargers don't reject a profile carrying `null` optionals. Needs charger smart-charging support.
  - **Connection supervision:** liveness is enforced at the **OCPP layer, not the WebSocket layer**. `OcppController` sets `KeepAliveInterval` (60s) to hold NATs/proxies open but deliberately leaves `KeepAliveTimeout` unset — setting it makes .NET ping-and-expect-pong and force-abort with `ConnectionAbortedException: The connection was aborted by the application`, which kills real chargers that never answer WS-level pings (they keep the link alive with OCPP Heartbeat instead). Dead-link detection: `OcppConnectionOrchestrator.ProcessMessagesAsync` bounds each `ReceiveAsync` with an idle timeout (`ResolveIdleTimeoutAsync` = `max(3 × HeartbeatInterval, 90s)`) — since a charger emits a Heartbeat every `HeartbeatInterval`, OCPP silence past that window means dead, so it aborts the socket and breaks. Do NOT re-add `KeepAliveTimeout`. `SessionManager.RegisterSession` returns the displaced session on reconnect and the orchestrator aborts its socket immediately (zombie cleanup). The on-connect config push also sets the `HeartbeatInterval` key explicitly (BootNotification's interval never reaches chargers that reconnect without rebooting) and skips keys already at the desired value.
  - Transaction ids are minted locally by `ChargePointSession`. Chargers retransmit `StartTransaction` when a reply is slow and re-announce an ongoing transaction after a reconnect — dedup exists in TWO places and both must stay: `ChargePointSession` answers a repeated StartTransaction (same connector + meterStart) with the same id, and `ChargeSessionRecorder` deletes still-open duplicate rows (same charger/connector/meterStart, older tx id) when a new one arrives.
  - Telemetry flows through `IChargerTelemetrySink`; `TelemetryFanout` (in Backend) fans out to `ChargerStatusTracker` (in-memory live status) and `ChargeSessionRecorder` (persists sessions + per-hour cost on transaction stop).
  - `IOcppChargerConfigurationProvider` is the seam that feeds `ChargerSettings` values into the on-connect config push without Backend.OCPP referencing the DB (implemented by `DbOcppChargerConfigurationProvider` in Backend).
- **HASmartCharge.Backend.HomeAssistant** — OAuth2 (indieauth flow, token refresh background services) + `IHomeAssistantControl` (read entity states/SoC, generic `CallServiceAsync(domain, service, dataJson)`, list entities/services). Car start/stop are *generic* HA service calls configured in CarSettings, so any actuator works (button, switch, script, …).
  - **HA OAuth client_id rule:** HA requires the token-refresh `client_id` to equal the client_id used at authorization — this app's own URL (`http://{host}` of the backend), NOT the HA base URL. It's persisted per connection (`HomeAssistantConnection.ClientId`). Getting this wrong makes every refresh fail with 400 `invalid_request` ~25 min after connect.
  - **HA refresh grant returns NO refresh token:** HA's `/auth/token` `refresh_token` grant responds with only `access_token`/`expires_in`/`token_type` — never a rotated refresh token (HA does not rotate). So `TokenResponse.RefreshToken` must stay optional (`string?`, not `required`) or System.Text.Json throws on every refresh; and `RefreshAccessTokenAsync` must keep the stored refresh token, never overwrite it with the (absent) response value. Only the `authorization_code` grant returns a refresh token.
  - **Token wipe policy:** stored tokens are only cleared on definitive rejection — `invalid_grant` on refresh. A 401/403 at startup verification is NOT definitive (the short-lived access token simply expired while the backend was offline): startup attempts a refresh first and only wipes if that refresh comes back `invalid_grant`. Transient failures (HA down, network, 5xx) must keep tokens and retry — don't reintroduce disconnect-on-any-error.
- **HASmartCharge.Backend** — ASP.NET Core API + background services: `PriceFetchService` (EPEX fetch on RefreshMinutes + a 13:05 Europe/Amsterdam wake-up for next-day prices), `ChargeOrchestratorService` (60s tick: recompute schedule, HA start/stop **only on state transitions**, manual-override window, MissedDeadline keeps charging), `ChargeSessionRecorder`. Controllers: settings, ha, prices, plan, charger, charge, sessions + OCPP WebSocket endpoint.
- **HASmartCharge.Backend.DB** — EF Core SQLite. Settings tables (`PriceProviderSettings`, `CarSettings`, `ChargerSettings`) are **single-row, seeded Id=1** via HasData; PUT endpoints update that row. `ChargeSession` PK = OCPP TransactionId (`ValueGeneratedNever`). `HomeAssistantConnection` stores OAuth tokens — don't reset the DB casually.
- **HASmartCharge.Frontend** — React 19 + Vite + TS + TanStack Query + recharts + Tailwind v4. Pattern: `src/types/*` mirror API DTOs, `src/api/*` one module per domain over `api/client.ts`, `src/hooks/*` TanStack Query wrappers. Pages: Dashboard, Schedule, Settings (tabs), History. `PriceChart` highlights selected charge hours.

## Conventions & gotchas

- **All times are UTC** end-to-end; hour buckets are UTC hour starts. SQLite round-trips `DateTime` as `Kind=Unspecified` — every controller re-stamps with `DateTime.SpecifyKind(..., Utc)` (see `PlanController.EnsureUtc`) so JSON always carries the `Z` suffix. Do the same in any new endpoint; frontend additionally guards via `ensureUtcSuffix()` in `lib/utils.ts`.
- **OCPP units**: OCPP 1.6 defaults energy measurands to **Wh when the unit field is missing**. `ChargerStatusTracker` and `ChargeSessionRecorder` both normalize missing/`Wh` units to kWh — keep that behavior.
- **Session energy is a delta**: the charger's `Energy.Active.Import.Register` is a lifetime total. Live session energy = current register − `ConnectorStatus.MeterStartKwh` (captured at StartTransaction); cost attribution likewise uses register deltas between samples.
- The EPEX API (`epexprijzen.nl`) returns **403 without a browser User-Agent**; `tomorrow` prices are empty until ~13:00 CET.
- OCPP-side services are **singletons**; `ApplicationDbContext` is scoped — singletons reach the DB through `IServiceScopeFactory` scope-per-call (see `DbOcppChargerConfigurationProvider`, `ChargeSessionRecorder`).
- Telemetry sink implementations must **never throw** into the OCPP session — wrap handlers in try/catch.
- Prices/money are `decimal`; energy is `double`; €/kWh displayed at 4 decimals, totals at 2.

## Verification pattern used in this repo

After backend changes: `dotnet build` + `dotnet test HASmartCharge.Core.Tests`, then boot the backend and curl the affected endpoints (they respond gracefully with HA/charger disconnected). After frontend changes: `npm run build` must exit 0.
