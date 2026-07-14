# Home Assistant MQTT Discovery: charging-statistics device + availability switch

## Context

HASmartCharge currently only *reads from* Home Assistant (states, SoC) and *calls services* (car start/stop). Nothing pushes charging telemetry back into HA, so charge stats can't be used on HA dashboards or in automations. This feature registers a **"HASmartCharge" device in HA via MQTT discovery** exposing 12 entities:

- Sensors: current power (kW), current charge % (car SoC), connector status, session energy (kWh), session cost, last heartbeat, plan deadline, plan target charge %, plan required kWh, plan estimated cost
- Binary sensor: chargepoint connected
- **Switch**: chargepoint Operative/Inoperative — mirrors the dashboard availability button *including* its enable/disable rule (via MQTT availability topic → greyed out in HA when toggling isn't allowed)

User decisions (confirmed via AskUserQuestion):
- Transport: **MQTT discovery** (user runs/will run broker + HA MQTT integration). REST `POST /api/states` rejected — stateless entities, no working button.
- "Current session power" = **session energy in kWh**.
- Control = **switch entity** (ON = Operative), not button(s).

## Data sources (verified in repo)

| Entity | Source |
|---|---|
| Current power kW | `ChargerStatusTracker.GetChargerStatus()` → `Measurands.PowerActiveImport`, W→kW like `ChargerController.ToKw` (ChargerController.cs:169-184) |
| Session energy kWh | `EnergyActiveImportRegister − ConnectorStatus.MeterStartKwh` clamped ≥0, only when `ActiveTransactionId` set (ChargerController.cs:56-62) |
| Session cost | `ChargeSessionRecorder.TryGetLiveCostAsync(txId)` (ChargeSessionRecorder.cs:158-187); null mid-session → unknown, not 0 |
| Connected | `ChargerStatus.IsConnected` |
| Connector status | `ConnectorStatus.Status` (9 OCPP 1.6 values Available…Faulted) |
| Last heartbeat | `ChargerStatus.LastUpdated` (= `/api/charger/status` `lastHeartbeatAt`) |
| Car SoC % | `IHomeAssistantControl.GetBatterySocAsync(CarSettings.HaSocEntityId)` (scoped; HTTP to HA — cache 30s). Fallback: OCPP `ConnectorMeasurands.SoC` when entity id empty |
| Plan fields | newest `ChargePlans` row with status `Pending`/`Active` (same filter as `GET /api/plan`, PlanController.cs:30-40; `MissedDeadline` excluded to match): `DeadlineUtc`, `TargetSocPercent`, `EstimatedEnergyKwh`, `EstimatedCost` |
| Currency unit for monetary sensors | `PriceProviderSettings.Currency` (PriceProviderSettings.cs:15, default "EUR") — not hardcoded |
| Availability command | `IChargerControl.SetConnectorAvailabilityAsync(chargePointId, connectorId, available)` (ChargerControl.cs:38-40) → `OcppCommandResult` |

Frontend rule to mirror (DashboardPage.tsx:214-247): set-unavailable allowed only when connector `Available`; set-available only when `Unavailable`; every other status or disconnected → control disabled.

## Design

### Placement, library, discovery style

- **All MQTT code in `HASmartCharge.Backend\Services\Mqtt\`** (no new project). The publisher aggregates state from Backend.OCPP + Backend + Backend.DB + Backend.HomeAssistant — only Backend sees all of them; same reason `TelemetryFanout`/`ChargeOrchestratorService` live there. A separate project would need "whole app state" inversion seams for zero reuse.
- **MQTTnet 4.3.7.1207, pinned** (last version with `ManagedMqttClient`: auto-reconnect, auto-resubscribe, outbound queue). v5 removed the managed client + renamed types (`MqttFactory`→`MqttClientFactory`); online v5 snippets won't compile against v4 — all MQTTnet types encapsulated in ONE class (`MqttConnection`) so future migration touches one file. net10.0 OK (v4 targets netstandard2.x).
- **Per-component discovery** (classic, battle-tested): one retained config per entity at `{discoveryPrefix}/<component>/hasmartcharge/<object_id>/config`. Shared `device.identifiers: ["hasmartcharge"]` block groups all 12 entities into one device. Not device-based discovery (younger, full-payload republish on any change).

### Topic tree (base topic + discovery prefix from `MqttSettings`, defaults shown)

```
hasmartcharge/status                       app availability (LWT) "online"/"offline", retained
hasmartcharge/charger/power_kw             ┐
hasmartcharge/charger/car_soc              │
hasmartcharge/charger/connected            │ "ON"/"OFF"
hasmartcharge/charger/connector_status     │ state topics, retained, QoS 1
hasmartcharge/charger/session_energy_kwh   │ empty payload = HA "unknown"
hasmartcharge/charger/session_cost         │
hasmartcharge/charger/last_heartbeat       │ ISO8601 Z
hasmartcharge/plan/deadline                │ ISO8601 Z
hasmartcharge/plan/target_soc              │
hasmartcharge/plan/required_kwh            │
hasmartcharge/plan/estimated_cost          ┘
hasmartcharge/switch/operative/state       "ON"/"OFF", retained
hasmartcharge/switch/operative/available   "online"/"offline", retained (enable/disable mirror)
hasmartcharge/switch/operative/set         command topic (subscribed)
homeassistant/status                       subscribed (HA birth message)
```

Node id fixed `hasmartcharge`, NOT `ChargePointId` (may be empty, topic-illegal chars, user-editable → renaming would orphan retained configs + duplicate the HA device). ChargePointId goes in device `model` info instead.

### Publishing strategy

One `BackgroundService` (`MqttPublisherService`), repo loop convention (try/catch tick body, separate OCE catch, scope-per-tick — model on ChargeOrchestratorService.cs:47-73). Loop wakes on **whichever comes first**:
1. 10s tick (`Task.Delay`) — covers plan/cost/SoC which aren't telemetry events,
2. telemetry nudge — `MqttTelemetryNudge : IChargerTelemetrySink` (bounded `Channel<bool>`, DropWrite; only `OnConnected`/`OnDisconnected`/`OnConnectorStatus` write; never throws) added as third sink to the `TelemetryFanout` array (Program.cs:62-64) → connected/connector/switch state react sub-second,
3. settings-change notify — `IMqttSettingsNotifier.WaitForChangeAsync` (see Settings below).

Per wake: load `MqttSettings` (scope); reconnect/rebuild `MqttConnection` if connection-relevant settings changed; build one immutable `MqttSnapshot` (all values pre-formatted as payload strings, `""` = unknown); **diff against per-topic last-published cache; publish only changes** (retained QoS 1). Nudge never publishes itself — exactly one publish path.

On (re)connect, ordered: discovery configs → subscribe `/set` + `homeassistant/status` → all states + switch availability → `hasmartcharge/status = "online"` LAST (HA never sees available entities without states). HA birth `online` → wait ~2s → republish everything. Discovery configs also republished when currency or base-topic/prefix settings change.

### Switch semantics (single shared static rule — command validation and state publishing can't diverge)

```
switchState     = connector?.Status == "Unavailable" ? OFF : ON
switchAvailable = charger.IsConnected && connector?.Status is "Available" or "Unavailable"
```

Switch discovery config uses dual availability + `availability_mode: "all"`: app LWT topic AND `switch/operative/available`. Command flow (`MqttAvailabilityCommandHandler`, `SemaphoreSlim(1,1)`, never throws):
1. Scope → `ChargerSettings` (ChargePointId, ConnectorId); unconfigured → snap-back, done.
2. **Server-side re-validation** with the shared rule (retained availability topic is advisory; raw `mosquitto_pub` must not bypass — note: existing `POST /api/charger/availability` has no guard, unchanged/out of scope). Not allowed or no-op → republish current state + availability (snap-back = republishing true retained state visually bounces HA's toggle).
3. `SetConnectorAvailabilityAsync(...)`; inspect `OcppCommandResult` status (reuse extracted `ReadStatus` helper, see refactor):
   - `Accepted` → publish new state optimistically; charger's follow-up StatusNotification → nudge → confirms + flips availability.
   - `Scheduled` (OCPP 1.6 legal) → do NOT flip; snap-back + log "scheduled by charger"; later StatusNotification drives state via nudge — no pending-command bookkeeping.
   - `Rejected`/CALLERROR/timeout → snap-back + warn.

### Entity metadata

| Entity | Component/object_id | device_class | unit | state_class | notes |
|---|---|---|---|---|---|
| Current power | sensor `power` | power | kW | measurement | `suggested_display_precision: 2` |
| Current charge | sensor `car_soc` | battery | % | measurement | precision 0 |
| Connected | binary_sensor `connected` | connectivity | — | — | `entity_category: diagnostic`; depends ONLY on app LWT — shows OFF when charger drops, never `unavailable` |
| Connector status | sensor `connector_status` | enum | — | — | `options`: the 9 OCPP 1.6 ChargePointStatus values |
| Session energy | sensor `session_energy` | energy | kWh | **omit** | resets per session: `total_increasing` would corrupt HA long-term stats; `measurement` disallowed with energy class. Code comment: lifetime register as separate `total_increasing` sensor = future option |
| Session cost | sensor `session_cost` | monetary | `PriceProviderSettings.Currency` | **omit** | precision 2 |
| Last heartbeat | sensor `last_heartbeat` | timestamp | — | — | ISO8601 Z; diagnostic |
| Plan deadline | sensor `plan_deadline` | timestamp | — | — | |
| Plan target | sensor `plan_target_soc` | — | % | — | no battery class (target, not level); `icon: mdi:battery-charging-90` |
| Plan required | sensor `plan_required_kwh` | energy | kWh | omit | |
| Plan est. cost | sensor `plan_estimated_cost` | monetary | Currency | omit | |
| Operative | switch `operative` | — | — | — | `icon: mdi:ev-station`, dual availability `all`, `optimistic: false` |

Rules: `timestamp`/`enum` classes must NOT carry unit/state_class (HA rejects config). All payloads: timestamps `DateTime.SpecifyKind(x, Utc).ToString("O")` (SQLite Kind=Unspecified re-stamp, same as `PlanController.EnsureUtc`); numbers `ToString(CultureInfo.InvariantCulture)`. Every config carries `unique_id`/`name`/`availability: [{topic: hasmartcharge/status}]`/`device` block/`origin` (sw_version from assembly version).

Example discovery config (current power) → retained to `homeassistant/sensor/hasmartcharge/power/config`:

```json
{
  "name": "Current power",
  "unique_id": "hasmartcharge_power",
  "state_topic": "hasmartcharge/charger/power_kw",
  "device_class": "power", "state_class": "measurement",
  "unit_of_measurement": "kW", "suggested_display_precision": 2,
  "availability": [ { "topic": "hasmartcharge/status" } ],
  "device": { "identifiers": ["hasmartcharge"], "name": "HASmartCharge",
              "manufacturer": "HASmartCharge", "model": "OCPP smart-charging bridge", "sw_version": "1.0.0" },
  "origin": { "name": "HASmartCharge", "sw": "1.0.0" }
}
```

### Resilience / lifecycle

- Disabled or blank host → publish `offline`, stop client, keep ticking cheaply (picks up enablement without restart).
- Broker down → ManagedMqttClient auto-reconnect (5s) + queued publishes (`MaxPendingMessages` ~500 drop-oldest; harmless — states retained). Log connect failures throttled to state changes, not every 5s.
- App dies → LWT flips all entities unavailable. Graceful `StopAsync` → publish `offline` explicitly (LWT only fires on ungraceful drops), stop client.
- Charger disconnected → `connected` OFF; charger-value sensors empty payload (`unknown`); `last_heartbeat` keeps last value; switch `offline`.
- No active plan → 4 plan topics empty payload.
- ChargePointId blank → device + discovery still published; charger entities unknown; plan entities work.
- Unparseable command payloads / HA birth `offline` → ignore, log debug.

### Settings storage & API

**`MqttSettings` entity** (new, `HASmartCharge.Backend.DB\Models\MqttSettings.cs`) — single-row Id=1, HasData seed like the other settings tables (`ApplicationDbContext.cs`: DbSet after line 21, seed after line 55):
`Enabled` (default **false** — zero behavior change until opt-in), `Host` ("core-mosquitto" — HA Mosquitto add-on host; dev types "localhost"), `Port` (1883), `Username`, `Password` (plaintext; repo precedent `HomeAssistantConnection` tokens, XML-doc note; no DPAPI — breaks Linux add-on container), `UseTls` (false), `ClientId` ("hasmartcharge"), `BaseTopic` ("hasmartcharge"), `DiscoveryPrefix` ("homeassistant").
Migration: `dotnet ef migrations add AddMqttSettings --project HASmartCharge.Backend.DB --startup-project HASmartCharge.Backend` (rebuild before `dotnet run` — PendingModelChangesWarning gotcha).

**API**
- `GET/PUT api/settings/mqtt` added to existing `SettingsController` (matches `api/settings/price|car|charger`, SettingsController.cs:77-106 pattern: entity IS the DTO, field-by-field copy, `FirstAsync`). PUT calls `IMqttSettingsNotifier.NotifyChanged()` AFTER `SaveChangesAsync`.
- New `MqttController` (`api/mqtt`):
  - `GET status` → `{ enabled, connected, host, port, lastConnectedAt, lastPublishAt, lastError, lastErrorAt }` from `IMqttPublisherStatus.GetSnapshot()` (shape mirrors HomeAssistantController.cs:25-36; timestamps in-memory UtcNow — no EnsureUtc needed, comment it).
  - `POST test` → `IMqttConnectionTester.TestAsync(savedSettings)` one-shot connect/disconnect → `{ success, error? }`.

**Seams** (`Services\Mqtt\`): `IMqttSettingsNotifier` (singleton; `NotifyChanged()` + `WaitForChangeAsync(ct)`, TCS swapped under lock), `IMqttPublisherStatus` (snapshot record, implemented by publisher), `IMqttConnectionTester`. Belt-and-braces: publisher also diffs settings every tick, so a missed signal self-heals.

Add-on `/data/options.json` `mqtt_*` overrides: **deferred phase 2** (documented, not built). Future: Supervisor `services: [mqtt:need]` auto-credentials.

### Frontend — new Settings tab "MQTT"

Mirrors existing patterns exactly:
- `src\types\settings.ts` — append `MqttSettings` interface (camelCase mirror); new `src\types\mqtt.ts` — `MqttStatus`.
- `src\api\settingsApi.ts` — `getMqttSettings`/`updateMqttSettings` (copy of lines 26-35); new `src\api\mqttApi.ts` — `getMqttStatus`, `testMqtt`.
- `src\hooks\useSettings.ts` — `settingsKeys.mqtt`, `useMqttSettings`/`useUpdateMqttSettings` (copy 42-52; onSuccess additionally invalidates mqtt status key); new `src\hooks\useMqtt.ts` — `useMqttStatus` (`refetchInterval: 10_000`), `useTestMqtt`.
- New `src\pages\settings\MqttTab.tsx` — form skeleton from `ChargerTab.tsx:22-57,70-153,207-216` (local form state seeded from query, saveError/savedAt banners, blue Save button); status card from `HomeAssistantTab.tsx:23-44` (Badge success+pulse "Connected" / danger, host:port, last publish, red lastError line); Enabled checkbox per `AutoScheduleCard.tsx:73-81`. Fields: Enabled, Host, Port, Username, Password (`type="password"`), UseTls, ClientId, BaseTopic, DiscoveryPrefix. "Test connection" secondary button + result banner; helper text "Tests the last saved settings."
- `src\pages\SettingsPage.tsx` — TABS entry `{ id: 'mqtt', label: 'MQTT', Component: MqttTab }` (lines 9-14), subtitle line 22.

## Files

**New — backend** (`HASmartCharge.Backend\Services\Mqtt\` unless noted)
| File | Responsibility |
|---|---|
| `MqttPublisherService.cs` | BackgroundService: lifecycle, wake on tick/nudge/settings-notify, snapshot→diff→publish, connect sequence, birth handling, StopAsync offline |
| `MqttConnection.cs` | ONLY file touching MQTTnet: ManagedMqttClient wrapper (LWT, auto-reconnect 5s, MaxPendingMessages), publish/subscribe, OnCommand/OnHaBirth callbacks, IAsyncDisposable |
| `MqttTopics.cs` | Topic/object_id builders from BaseTopic + DiscoveryPrefix |
| `HaDiscoveryConfigBuilder.cs` | **Pure static**: settings + currency in → 12 (topic, json) config pairs out. Unit-testable |
| `MqttSnapshotBuilder.cs` | Singleton, scope-per-call: DB (ChargerSettings, plan, currency) + tracker + live cost + 30s-cached SoC → immutable `MqttSnapshot` of pre-formatted payload strings; shared switch-rule static method |
| `MqttAvailabilityCommandHandler.cs` | Command flow §switch semantics; SemaphoreSlim; never throws |
| `MqttTelemetryNudge.cs` | IChargerTelemetrySink → bounded channel wake signal |
| `MqttSeams.cs` | `IMqttSettingsNotifier`+impl, `IMqttPublisherStatus`+snapshot record, `IMqttConnectionTester`+impl |
| `..\Controllers\MqttController.cs` | GET api/mqtt/status, POST api/mqtt/test |
| `..DB\Models\MqttSettings.cs` + generated migration | entity + Id=1 seed |
| `HASmartCharge.Backend.Tests\` (new xunit project, mirrors Core.Tests.csproj, added to slnx) | tests for HaDiscoveryConfigBuilder + switch rule (Core.Tests can't reference Backend; Core stays dependency-free) |

**Modified — backend**
| File | Change |
|---|---|
| `HASmartCharge.Backend.csproj` | `<PackageReference Include="MQTTnet" Version="4.3.7.1207" />` (pinned) |
| `Program.cs` | Register MqttTelemetryNudge/MqttSnapshotBuilder/MqttAvailabilityCommandHandler/notifier/tester singletons + `AddHostedService<MqttPublisherService>`; add nudge as 3rd sink in TelemetryFanout array (lines 62-64) |
| `ApplicationDbContext.cs` | DbSet + HasData seed |
| `Controllers\SettingsController.cs` | GET/PUT mqtt + notifier injection |
| `Backend.OCPP` (small refactor) | Extract `ChargerController.ToKw` (169-184) and `ReadStatus` (187-198) into shared static helpers in Backend.OCPP; ChargerController + MQTT both use them |

**Frontend**: new `src\types\mqtt.ts`, `src\api\mqttApi.ts`, `src\hooks\useMqtt.ts`, `src\pages\settings\MqttTab.tsx`; modified `src\types\settings.ts`, `src\api\settingsApi.ts`, `src\hooks\useSettings.ts`, `src\pages\SettingsPage.tsx`.

## Implementation order

1. MQTTnet package + `ToKw`/`ReadStatus` helper extraction (build stays green).
2. `MqttSettings` entity + DbSet + seed + migration; SettingsController GET/PUT + notifier; rebuild.
3. `MqttTopics` + `HaDiscoveryConfigBuilder` (pure) + Backend.Tests project with builder/switch-rule tests.
4. `MqttSnapshotBuilder` + snapshot record.
5. `MqttConnection` wrapper.
6. `MqttPublisherService` (loop, connect sequence, birth, status snapshot).
7. `MqttAvailabilityCommandHandler` + `MqttTelemetryNudge` + Program.cs wiring.
8. `MqttController` (status/test).
9. Frontend tab + api/hooks/types.

## Verification

1. `dotnet build HASmartCharge.slnx` → 0 errors/0 warnings; `dotnet test HASmartCharge.Core.Tests` + new `HASmartCharge.Backend.Tests` green. (Rider debugger file-lock caveat: ask user to stop debug session.)
2. Local broker: `docker run -d --name mqtt-dev -p 1883:1883 eclipse-mosquitto:2 mosquitto -c /mosquitto-no-auth.conf`; watch: `docker exec -it mqtt-dev mosquitto_sub -t 'homeassistant/#' -t 'hasmartcharge/#' -v`.
3. Boot backend (port 5293):
   - `GET /api/settings/mqtt` → seed row (enabled:false, host core-mosquitto).
   - `GET /api/mqtt/status` → graceful `{ enabled:false, connected:false }`.
   - `PUT /api/settings/mqtt` (enabled:true, host localhost) → within ~1 tick: retained discovery configs (12), states, `hasmartcharge/status = online` in mosquitto_sub; status endpoint connected:true; `POST /api/mqtt/test` → success.
   - Negative: PUT port 1884 → connected:false + lastError; PUT back → reconnects WITHOUT backend restart (proves notify seam).
   - No charger attached (no OCPP simulator exists in repo — fine): charger sensors publish empty/unknown, plan sensors work, no crash.
   - Switch: `docker exec mqtt-dev mosquitto_pub -t 'hasmartcharge/switch/operative/set' -m ON` with no charger → snap-back republish of state observed (server-side guard works without HA).
4. Frontend: `npm run build` exit 0; manual `npm run dev` → MQTT tab load/save/test/badge flip.
5. Optional E2E: HA container (`ghcr.io/home-assistant/home-assistant:stable`) + MQTT integration → device "HASmartCharge" with 12 entities; toggle switch in each connector state (Available/Charging/Unavailable/disconnected — greyed correctly); kill backend → LWT flips everything unavailable; restart → online.
