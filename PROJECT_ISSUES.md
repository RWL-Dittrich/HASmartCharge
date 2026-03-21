# Architectural Review Report — HASmartCharge

---

## 1. Executive Summary

This codebase is a young, actively-developed system for managing EV charging stations via OCPP 1.6J, with a Home Assistant integration and a React frontend. It is structured into four .NET projects (Backend, Backend.OCPP, Backend.DB, AppHost) plus a Vite/React frontend.

**The good news:** there is already an attempt at layering — the OCPP project has Transport, Domain, Application, Services, and Infrastructure folders, and an `IOcppPersistence` interface correctly decouples the OCPP module from EF Core. The WebSocket transport is abstracted behind `IConnection`. The frontend is clean and well-structured with React Query.

**The bad news:** the layering is cosmetic. The "Domain" layer in OCPP is not a domain layer — it's a protocol-handling layer that directly contains OCPP message parsing, JSON deserialization, and side effects (persistence calls, status tracker updates). There is no actual domain model — no `Charger` entity with behavior, no `ChargingSession` aggregate, no domain events. The system is architecturally defined by OCPP; adding a second protocol would require duplicating most of the business logic. The HA integration is a separate island with no connection to the core domain. The Backend project is a composition root that also houses HA auth infrastructure, background services, and API controllers that directly depend on OCPP-specific types.

**Bottom line:** The system works for OCPP 1.6 today, but it has no extensibility path. Adding a second protocol, a second integration target, or non-trivial business rules (smart charging schedules, tariff logic, load balancing) will require either a significant refactor or duplicating logic across protocol adapters.

---

## 2. Current Architecture Assessment

### Inferred Architecture

```
┌─────────────────────────────────────────────────────┐
│  HASmartCharge.AppHost (.NET Aspire)                │
│  Composition root for local development              │
└────────────┬────────────────────────────────────────┘
             │ references
┌────────────▼────────────────────────────────────────┐
│  HASmartCharge.Backend (ASP.NET Core Web API)       │
│  ├─ Controllers/                                     │
│  │   ├─ OcppController          → WebSocket accept   │
│  │   ├─ ChargersController      → reads StatusTracker│
│  │   ├─ ChargerCommandsController → writes sessions  │
│  │   ├─ DashboardController     → reads StatusTracker│
│  │   └─ HomeAssistantAuthController → HA OAuth       │
│  ├─ Services/Auth/              → HA token management│
│  ├─ BackgroundServices/         → timer-based cleanup │
│  ├─ Models/                     → DTOs for HA, Auth  │
│  └─ Program.cs                  → DI + startup       │
└───────┬──────────────────────────┬──────────────────┘
        │ references               │ references
┌───────▼──────────┐    ┌─────────▼──────────────────┐
│ Backend.OCPP     │    │ Backend.DB                  │
│ ├─ Transport/    │    │ ├─ ApplicationDbContext      │
│ ├─ Domain/       │    │ ├─ OcppRepository           │
│ ├─ Application/  │    │ ├─ Models/ (EF entities)    │
│ ├─ Services/     │    │ └─ Migrations/              │
│ ├─ Models/       │    └────────────────────────────┘
│ └─ Infrastructure│
└──────────────────┘
```

**Project dependency graph:**
- Backend → Backend.OCPP, Backend.DB
- Backend.DB → Backend.OCPP (for `IOcppPersistence`)
- Backend.OCPP → nothing (standalone)

### Key observation
Backend.DB references Backend.OCPP to implement `IOcppPersistence`. This inverts the dependency correctly (OCPP defines the interface, DB implements it). But it means OCPP-specific DTOs (`PersistedCharger`, `OcppBootInfo`) are defined in the OCPP project and used by the DB project. This makes the "persistence abstraction" OCPP-specific by definition.

---

## 3. Key Architectural Problems

### Problem 1: No Domain Layer Exists
**Smell:** There are no domain entities with behavior anywhere. `ChargerStatus`, `ConnectorStatus`, `ConnectorMeasurands` in Backend.OCPP are mutable data bags in a `Services/` or `Models/` folder. The DB models (`Charger`, `Connector`, `ChargingTransaction`) are anemic EF entities.

**Root cause:** The system was built protocol-first rather than domain-first. The OCPP message flow became the architecture.

**Consequence:** Business logic (what happens when a transaction starts, how to authorize, when to accept/reject) is embedded in `ChargePointSession.HandleStartTransactionAsync()` — inside a protocol handler. Any new protocol would need to re-implement these decisions.

### Problem 2: `ChargePointSession` Is a God Object
**Smell:** ChargePointSession.cs is ~550 lines and handles:
- OCPP message deserialization
- Business logic (accept/reject authorization, transaction tracking)
- Side effects (persistence calls, status tracker updates)
- Outbound command sending with correlation tracking
- Charger initialization/configuration orchestration

**Root cause:** No separation between protocol translation and domain behavior.

**Consequence:** Untestable without mocking 6+ dependencies. Every OCPP protocol change risks breaking business logic. Cannot reuse transaction/authorization logic for a non-OCPP protocol.

### Problem 3: `ChargerStatusTracker` Is a Second God Object
**Smell:** ChargerStatusTracker.cs is ~460 lines and is:
- The in-memory read model for all charger/connector/measurand state
- The write model (mutated by protocol handlers)
- Queried directly by REST controllers
- Seeded from the database at startup
- Lives in Backend.OCPP but is consumed by Backend controllers

**Root cause:** There's no CQRS-like separation between the write path (protocol events update state) and the read path (API queries state). Everything goes through one mutable concurrent dictionary.

**Consequence:** Thread-safety concerns are managed by `ConcurrentDictionary`, but individual `ChargerStatus` objects are mutated without locking. Controllers in Backend have a hard dependency on an OCPP-specific service. Cannot swap the read model implementation.

### Problem 4: Backend Controllers Directly Depend on OCPP Types
**Smell:** `ChargersController`, `DashboardController`, and `ChargerCommandsController` import and directly use types from `HASmartCharge.Backend.OCPP.Models` and `HASmartCharge.Backend.OCPP.Services`.

**Root cause:** No application layer or abstraction sits between the API layer and the protocol layer.

**Consequence:** The REST API is structurally coupled to OCPP. Adding a second protocol means either the controllers need to understand both protocols, or the entire query/command path needs to be rearchitected.

### Problem 5: Home Assistant Integration Is Architecturally Isolated but Structurally Tangled
**Smell:** The HA integration lives in Backend (services, auth, models) with its own OAuth flow, token management, and API client. But it has zero interaction with the charging domain — it can list HA entities but does nothing with chargers.

**Root cause:** HA integration was built as a separate feature track with no connection to the core domain model.

**Consequence:** When you eventually need HA to publish charger states as HA entities then read HA sensors for smart charging decisions, there's no integration layer or event system to connect these concerns. You'll be tempted to inject HA services directly into protocol handlers, creating a tight coupling between HA and OCPP.

### Problem 6: The "Domain" Folder in Backend.OCPP Is Not a Domain Layer
**Smell:** `Backend.OCPP/Domain/` contains `ChargePointSession` (a protocol handler), `SessionManager` (a connection registry), and their interfaces. These are infrastructure / application concerns dressed up as domain.

**Root cause:** DDD terminology was applied to OCPP machinery rather than to actual business concepts.

**Consequence:** Misleading architecture. Developers will put real domain logic in this folder because it's called "Domain."

### Problem 7: No Domain Events or Integration Events
**Smell:** When a charger connects, boots, starts a transaction, or reports meter values, the `ChargePointSession` directly calls `_statusTracker.OnFoo()` and `_persistence.FooAsync()`. There is no event mechanism.

**Root cause:** Procedural control flow — each handler manually calls each side-effect recipient.

**Consequence:** Adding a new side effect (e.g., "notify HA when a transaction starts") requires modifying `ChargePointSession` — violating Open/Closed Principle. Every protocol adapter would need the same side-effect wiring.

### Problem 8: Configuration Management for Docker Deployment
**Smell:** Program.cs has hardcoded service registration, manual migration logic at startup, and `Console.WriteLine` for startup diagnostics.

**Root cause:** Early-stage code with tight coupling between composition and runtime behavior.

**Consequence:** Moving to Docker/HA Add-on requires environment-based configuration for DB paths, HA URLs, OCPP ports, etc. Current code has some of this via appsettings.json but startup logic does direct `Console.WriteLine` instead of structured logging.

---

## 4. DDD Gap Analysis

| DDD Concept | Current State | Assessment |
|---|---|---|
| **Entities** | `Charger`, `Connector`, `ChargingTransaction` — anemic EF models with zero behavior | Missing |
| **Value Objects** | None. `MeasurandValue` is close but mutable, lacks equality semantics | Missing |
| **Aggregates** | None. No aggregate root protects invariants | Missing |
| **Domain Events** | None. Side effects are procedurally wired | Missing |
| **Repositories** | `OcppRepository` implements `IOcppPersistence` with OCPP-specific operations — it's a persistence service, not a domain repository | Incorrect abstraction |
| **Domain Services** | None. Business decisions (authorize, accept transaction) are embedded in `ChargePointSession` | Missing |
| **Application Services / Use Cases** | None. Controllers talk directly to protocol handlers and in-memory trackers | Missing |
| **Bounded Contexts** | Implicitly: Charging (chargers, connectors, transactions), Home Assistant Integration, OCPP Protocol. Not explicitly bounded or isolated | Implicit but not enforced |

### What the actual domain concepts are:
1. **Charger** (aggregate root) — identity, hardware info, configuration, connection state
2. **Connector** — belongs to a Charger, has availability and status
3. **ChargingSession** (aggregate root) — lifecycle from start to stop, meter readings, authorization
4. **Authorization** — who is allowed to charge, authorization decisions
5. **MeterReading** (value object) — energy, power, voltage, current at a point in time
6. **ChargerConfiguration** — key-value pairs, protocol-agnostic desired state

### What should NOT be in the domain:
- OCPP message parsing/serialization
- WebSocket connection management
- Home Assistant OAuth flow
- JSON deserialization
- EF Core entities/DbContext
- In-memory caching of charger state (`ChargerStatusTracker`)

---

## 5. Recommended Target Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                    HASmartCharge.Host                             │
│  ASP.NET Core composition root                                   │
│  ├─ Controllers/ (thin — delegates to Application layer)        │
│  ├─ Program.cs (DI wiring only)                                 │
│  └─ Docker/HA Add-on configuration                              │
└───────┬──────────────────────────────────────────────────────────┘
        │ depends on
┌───────▼──────────────────────────────────────────────────────────┐
│              HASmartCharge.Application                            │
│  Application Services / Use Cases / Query Handlers               │
│  ├─ Commands/ (StartChargingSession, StopChargingSession, etc.) │
│  ├─ Queries/ (GetChargerStatus, GetDashboardSummary, etc.)      │
│  ├─ Ports/ (interfaces consumed by adapters)                    │
│  │   ├─ IChargerGateway (protocol-agnostic send commands)       │
│  │   ├─ IChargerRepository                                       │
│  │   ├─ IChargingSessionRepository                               │
│  │   ├─ IChargerReadModel (query side)                           │
│  │   ├─ IHomeAutomationGateway (read/write HA state)            │
│  │   └─ IDomainEventPublisher                                    │
│  └─ EventHandlers/                                               │
└───────┬──────────────────────────────────────────────────────────┘
        │ depends on
┌───────▼──────────────────────────────────────────────────────────┐
│              HASmartCharge.Domain                                 │
│  Pure domain model — zero infrastructure dependencies            │
│  ├─ Charger.cs (aggregate root)                                  │
│  ├─ Connector.cs (entity under Charger)                          │
│  ├─ ChargingSession.cs (aggregate root)                          │
│  ├─ MeterReading.cs (value object)                               │
│  ├─ Authorization/ (domain service)                              │
│  └─ Events/ (ChargerConnected, TransactionStarted, etc.)        │
└──────────────────────────────────────────────────────────────────┘

            ┌────────────────────┐
            │  Adapter Projects  │ (implement Application.Ports interfaces)
            └────────────────────┘
┌───────────────────┐  ┌────────────────────┐  ┌───────────────────┐
│ Adapters.Ocpp     │  │ Adapters.Database   │  │ Adapters.HA       │
│ OCPP 1.6 protocol │  │ EF Core, SQLite     │  │ HA REST API,      │
│ WebSocket handler │  │ Repositories        │  │ OAuth, Entity     │
│ Message parsing   │  │ Read model impl     │  │ mapping           │
│ Protocol-specific │  │                     │  │                   │
│ types             │  │                     │  │                   │
└───────────────────┘  └─────────────────────┘  └───────────────────┘
```

**Key principle:** The domain and application layers define _what_ happens. Adapters define _how_ it happens through specific technologies.

---

## 6. Proposed Module / Layer Responsibilities

### HASmartCharge.Domain
- **Zero NuGet dependencies** (not even Microsoft.Extensions.Logging)
- Contains: Charger aggregate, Connector entity, ChargingSession aggregate, MeterReading value object, Authorization domain service, Domain events
- All business invariants enforced here (e.g., "cannot start transaction on an unavailable connector")

### HASmartCharge.Application
- References: Domain only
- Contains: Command/query handlers, port interfaces (for inbound/outbound), event handlers, application-level orchestration
- Defines `IChargerGateway` (send commands to a charger, protocol-agnostic), `IChargerRepository`, `IChargingSessionRepository`, `IChargerReadModel`, `IHomeAutomationGateway`
- No OCPP types, no HA types, no EF types

### HASmartCharge.Adapters.Ocpp
- References: Application (for ports), Domain (for events/entities)
- Contains: OCPP 1.6 WebSocket handling, message parsing, `OcppChargerGateway` implementing `IChargerGateway`, OCPP-specific models, protocol-to-domain translation
- The current `ChargePointSession` split into: OcppConnectionHandler (transport) + OcppMessageTranslator (protocol-to-domain mapping)

### HASmartCharge.Adapters.Database
- References: Application (for repository interfaces), Domain (for entities)
- Contains: `ApplicationDbContext`, EF entities (separate from domain entities — or shared if pragmatic), repository implementations, read model implementation

### HASmartCharge.Adapters.HomeAssistant
- References: Application (for `IHomeAutomationGateway`)
- Contains: HA OAuth, token management, REST API client, entity mapping, state syncing

### HASmartCharge.Host
- References: All of the above (for DI wiring)
- Contains: ASP.NET controllers (thin), Program.cs, appsettings, Dockerfile, HA Add-on config

---

## 7. Protocol Extensibility Strategy

### Current Problem
OCPP defines the entire internal model. `ChargerStatus`, `ConnectorStatus`, `ConnectorMeasurands`, `BootNotificationRequest`, etc. are all OCPP-specific types used throughout the system. The controllers return OCPP model types directly.

### Target
Each protocol adapter must translate protocol-specific messages into domain commands/events:

```
OCPP BootNotification  →  Application Command: RegisterCharger(chargePointId, vendor, model, ...)
OCPP StartTransaction  →  Application Command: BeginChargingSession(chargerId, connectorId, idTag, ...)
OCPP MeterValues       →  Application Command: ReportMeterValues(chargerId, connectorId, readings[])
OCPP StatusNotification→  Application Command: UpdateConnectorStatus(chargerId, connectorId, status)
```

### Abstractions needed

1. **`IChargerGateway`** (outbound port, in Application layer):
```csharp
public interface IChargerGateway
{
    Task<CommandResult> ResetChargerAsync(string chargerId, ResetType type, CancellationToken ct);
    Task<CommandResult> SetAvailabilityAsync(string chargerId, int connectorId, bool available, CancellationToken ct);
    Task<CommandResult> StartTransactionAsync(string chargerId, int connectorId, string idTag, CancellationToken ct);
    Task<CommandResult> StopTransactionAsync(string chargerId, int transactionId, CancellationToken ct);
    // ... other domain-level operations, NOT OCPP operations
}
```

2. **`IProtocolAdapter`** (optional, for lifecycle management):
```csharp
public interface IProtocolAdapter
{
    string ProtocolName { get; }
    Task StartAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);
}
```

3. **Domain events** as the integration mechanism:
   - `ChargerConnectedEvent` → triggers read model update, HA state publish
   - `ChargingSessionStartedEvent` → triggers read model update, HA state publish
   - When a future protocol (e.g., ISO 15118, OCPP 2.0.1, proprietary API) is added, it produces the same domain events and consumes the same `IChargerGateway` commands.

### What must move out of Backend.OCPP
- `ChargerStatusTracker` → becomes a general read model implementation, not OCPP-specific
- `ChargerStatus`, `ConnectorStatus`, `ConnectorMeasurands` → the domain-relevant parts become domain entities/value objects; the OCPP-specific parts stay in the OCPP adapter
- `IOcppPersistence` → becomes domain-level `IChargerRepository` + `IChargingSessionRepository`

---

## 8. Home Assistant Integration Strategy

### Current State
- OAuth flow fully implemented (HomeAssistantAuthService.cs, HomeAssistantConnectionManager.cs)
- Token refresh via background service (TokenRefreshService.cs)
- API client can list entities (HomeAssistantApiService.cs)
- No actual integration: charger data is never pushed to HA, HA sensor data is never consumed

### Recommended Architecture

**Anti-corruption layer:** `IHomeAutomationGateway` in the Application layer:
```csharp
public interface IHomeAutomationGateway
{
    Task PublishChargerStateAsync(string chargerId, ChargerState state, CancellationToken ct);
    Task PublishChargingSessionAsync(string sessionId, SessionState state, CancellationToken ct);
    Task<decimal?> ReadSensorValueAsync(string sensorId, CancellationToken ct);
    Task CallServiceAsync(string domain, string service, object data, CancellationToken ct);
}
```

**Implementation** in `Adapters.HomeAssistant`:
- Implements `IHomeAutomationGateway`
- Translates domain concepts to HA entity IDs and state formats
- Manages HA auth/tokens internally (opaque to the rest of the system)
- Subscribes to domain events (e.g., `ChargerConnectedEvent`) and publishes HA state updates

**What moves:**
- Everything in `Backend/Services/Auth/` → `Adapters.HomeAssistant`
- Everything in `Backend/Models/Auth/`, `Backend/Models/HomeAssistant/` → `Adapters.HomeAssistant`
- `Backend/BackgroundServices/TokenRefreshService.cs` → `Adapters.HomeAssistant`
- `Backend/BackgroundServices/AuthStateCleanupService.cs` → `Adapters.HomeAssistant`
- `Backend/Controllers/HomeAssistantAuthController.cs` → `Host` (if the auth UI stays as a REST endpoint) or `Adapters.HomeAssistant` (if exposed via HA Add-on ingress)
- `Backend/Configuration/HomeAssistantAuthOptions.cs` → `Adapters.HomeAssistant`
- `Backend.DB/Models/HomeAssistantConnection.cs` → `Adapters.HomeAssistant` (with its own DbContext or separate persistence)

---

## 9. Deployment / Add-on / Docker Considerations

### Current Issues
1. **Aspire AppHost** is used for dev orchestration — good for development, but irrelevant for Docker deployment. No Dockerfile or HA Add-on config exists yet.
2. **Database path** is hardcoded to a relative path (`../hasmartcharge.db`). In Docker, this needs to be a mounted volume path configured via environment variable.
3. **Startup logic in Program.cs** runs migrations + seeds the status tracker + initializes HA connection — all synchronously in the request pipeline startup. In Docker, if HA is not yet available, startup fails.
4. **No health checks** for charger connectivity or HA connection status.
5. **`Console.WriteLine`** instead of `ILogger` for startup diagnostics.

### Recommendations
1. Create a `Dockerfile` and HA Add-on `config.yaml` in an `addon/` folder.
2. All external configuration (DB path, HA URL, OCPP port) via environment variables with appsettings.json as defaults.
3. Startup initialization (HA connection, DB migration) should be resilient — retry with backoff, don't block app startup.
4. Add ASP.NET health checks (`/health`) for: DB connectivity, HA connection status, OCPP listener status.
5. Replace `Console.WriteLine` with structured logging throughout.
6. Consider separating the frontend build into the Docker image (multi-stage build) or keep it as a separate container proxied through HA ingress.

---

## 10. Refactoring Roadmap (Phased)

### Phase 1: Foundation (Quick Wins)
*Goal: Establish boundaries without breaking existing functionality.*

1. **Create `HASmartCharge.Domain` project** — empty initially, then incrementally move entity definitions in.
2. **Create `HASmartCharge.Application` project** — define port interfaces (`IChargerReadModel`, `IChargerRepository`, `IChargingSessionRepository`).
3. **Extract `ChargerStatusTracker` out of Backend.OCPP** — it is not OCPP-specific. Move to Application (as an in-memory implementation of `IChargerReadModel`) or a shared project.
4. **Replace `Console.WriteLine` with `ILogger`** in Program.cs.
5. **Make controllers depend on application interfaces**, not OCPP types directly.

### Phase 2: Domain Model (Medium Effort)
*Goal: Establish a real domain model that owns business logic.*

6. **Define domain entities** — `Charger`, `Connector`, `ChargingSession` with behavior (accept/reject authorization, start/stop session, validate connector availability).
7. **Define domain events** — `ChargerConnected`, `ChargerDisconnected`, `ChargingSessionStarted`, `ChargingSessionStopped`, `MeterValuesReceived`, `ConnectorStatusChanged`.
8. **Introduce a simple in-process event bus** (or use MediatR) for domain event dispatch.
9. **Refactor `ChargePointSession`** — split into:
   - `OcppMessageHandler` (translates OCPP messages to domain commands)
   - Domain commands flow through Application layer
   - Side effects (persistence, read model update) triggered by domain events

### Phase 3: Adapter Isolation (Medium–High Effort)
*Goal: OCPP becomes one adapter among many.*

10. **Rename `Backend.OCPP` to `Adapters.Ocpp`** and remove all non-OCPP responsibilities.
11. **Move `ChargerStatus`-related models** — split into domain-level state (in Domain) and OCPP-specific DTOs (in Adapters.Ocpp).
12. **Define `IChargerGateway`** in Application — implement as `OcppChargerGateway` in Adapters.Ocpp.
13. **Rename `Backend.DB` to `Adapters.Database`** — implement repository interfaces from Application layer.
14. **Extract HA integration** into `Adapters.HomeAssistant` — move auth, token management, API client, background services.
15. **Implement `IHomeAutomationGateway`** for HA.

### Phase 4: Deployment & Polish (Medium Effort)
*Goal: Production-ready for HA Add-on deployment.*

16. **Create Dockerfile** — multi-stage build for backend + frontend.
17. **Create HA Add-on config** (`config.yaml`, `run.sh`/entrypoint).
18. **Add health checks** — DB, HA, OCPP listener.
19. **Resilient startup** — HA initialization with retry, DB migration with retry.
20. **Environment-based configuration** — override all paths/URLs via env vars.

---

## 11. Priority-Ordered Task List

| # | Task | Effort | Phase |
|---|------|--------|-------|
| 1 | Create `HASmartCharge.Domain` project (empty, add to solution) | Small | 1 |
| 2 | Create `HASmartCharge.Application` project (empty, add to solution) | Small | 1 |
| 3 | Define `IChargerReadModel` interface in Application with methods matching current `ChargerStatusTracker` query methods | Small | 1 |
| 4 | Make `ChargersController` and `DashboardController` depend on `IChargerReadModel` instead of `ChargerStatusTracker` directly | Small | 1 |
| 5 | Make `ChargerStatusTracker` implement `IChargerReadModel`; register in DI as the interface | Small | 1 |
| 6 | Move `ChargerStatusTracker` from Backend.OCPP.Services to Application (or a shared project) — it is protocol-agnostic read state | Medium | 1 |
| 7 | Replace all `Console.WriteLine` in Program.cs with `ILogger` | Small | 1 |
| 8 | Define domain entities in `HASmartCharge.Domain`: `Charger` (aggregate root with Id, Vendor, Model, etc.), `Connector` (entity), `ChargingSession` (aggregate root) | Medium | 2 |
| 9 | Define domain events: `ChargerRegistered`, `ConnectorStatusUpdated`, `ChargingSessionStarted`, `ChargingSessionCompleted`, `MeterValuesReported` | Medium | 2 |
| 10 | Define `IChargerRepository` and `IChargingSessionRepository` interfaces in Application (replacing `IOcppPersistence`) | Medium | 2 |
| 11 | Create application command handlers: `RegisterChargerHandler`, `UpdateConnectorStatusHandler`, `BeginChargingSessionHandler`, `CompleteChargingSessionHandler`, `ReportMeterValuesHandler` | Medium | 2 |
| 12 | Refactor `ChargePointSession.HandleBootNotificationAsync` to translate OCPP message → call `RegisterChargerHandler` | Medium | 2 |
| 13 | Refactor `ChargePointSession.HandleStartTransactionAsync` to translate OCPP message → call `BeginChargingSessionHandler` | Medium | 2 |
| 14 | Refactor `ChargePointSession.HandleStopTransactionAsync` → `CompleteChargingSessionHandler` | Medium | 2 |
| 15 | Refactor `ChargePointSession.HandleStatusNotificationAsync` → `UpdateConnectorStatusHandler` | Medium | 2 |
| 16 | Refactor `ChargePointSession.HandleMeterValuesAsync` → `ReportMeterValuesHandler` | Medium | 2 |
| 17 | Make read model updates happen via domain event handlers instead of direct `_statusTracker.OnFoo()` calls | Medium | 2 |
| 18 | Make persistence calls happen via domain event handlers instead of direct `_persistence.FooAsync()` calls | Medium | 2 |
| 19 | Define `IChargerGateway` in Application layer for outbound commands (Reset, SetAvailability, StartTransaction, StopTransaction) | Small | 3 |
| 20 | Implement `OcppChargerGateway` in Backend.OCPP that delegates to `SessionManager`/`ChargePointSession` | Medium | 3 |
| 21 | Make `ChargerCommandsController` depend on `IChargerGateway` instead of `ISessionManager` | Medium | 3 |
| 22 | Rename `Backend.OCPP` to `Adapters.Ocpp`, adjust namespaces | Small | 3 |
| 23 | Rename `Backend.DB` to `Adapters.Database`, adjust namespaces | Small | 3 |
| 24 | Implement `IChargerRepository` and `IChargingSessionRepository` in Adapters.Database (replacing `IOcppPersistence` / `OcppRepository`) | Medium | 3 |
| 25 | Create `Adapters.HomeAssistant` project | Small | 3 |
| 26 | Move all HA auth services, background services, models, configuration from Backend into `Adapters.HomeAssistant` | Medium | 3 |
| 27 | Define `IHomeAutomationGateway` in Application layer | Small | 3 |
| 28 | Implement `IHomeAutomationGateway` in `Adapters.HomeAssistant` | Medium | 3 |
| 29 | Wire HA entity publishing into domain event handlers (publish charger state to HA on connect/disconnect/status change) | Medium | 3 |
| 30 | Create Dockerfile for production deployment | Medium | 4 |
| 31 | Create HA Add-on config.yaml, run script | Medium | 4 |
| 32 | Add ASP.NET health check endpoints | Small | 4 |
| 33 | Make startup initialization resilient (HA retry, DB migration retry) | Medium | 4 |
| 34 | Externalize all config via environment variables | Small | 4 |
| 35 | Add unit tests for Domain entities and Application command handlers | Medium | 4 |
| 36 | Add integration tests for Adapters.Ocpp protocol translation | Medium | 4 |

---

## 12. Final Summary Table of Required Changes

| Priority | Area | Current Problem | Recommended Change | Why It Matters | Expected Impact | Suggested Phase |
|----------|------|----------------|-------------------|---------------|----------------|-----------------|
| **P0** | Architecture | No domain layer exists | Create `HASmartCharge.Domain` with entities, aggregates, value objects, events | Without a domain layer, business logic is scattered across protocol handlers. Every new protocol duplicates decisions. | Enables protocol independence, testability, rule centralization | Phase 1–2 |
| **P0** | Architecture | No application layer exists | Create `HASmartCharge.Application` with command/query handlers and port interfaces | Controllers and protocol handlers directly couple to each other. No place for orchestration logic. | Clean separation of what vs how. Protocol-agnostic API layer. | Phase 1–2 |
| **P1** | God Object | `ChargePointSession` handles deserialization, business logic, persistence, status tracking, command sending | Split into OcppMessageHandler (protocol) + Application command handlers (business logic) + Domain event handlers (side effects) | 550-line class is untestable, un-extensible, and mixes all concerns | Each class has one reason to change; testable in isolation | Phase 2 |
| **P1** | God Object | `ChargerStatusTracker` is an OCPP-specific service used directly by non-OCPP controllers | Extract `IChargerReadModel` interface; make tracker protocol-agnostic and move up | Controllers are coupled to OCPP project. Read model cannot be replaced. | Protocol-independent query layer. Swappable read model. | Phase 1 |
| **P1** | Coupling | Controllers import OCPP types directly | Introduce application-layer abstractions; controllers depend only on Application interfaces | Adding a second protocol breaks the entire API surface or requires protocol-aware controllers | API becomes protocol-agnostic | Phase 1–3 |
| **P1** | Events | No domain event mechanism; side effects are procedurally wired | Introduce domain events + in-process event bus | Adding any new side effect (notify HA, log metric, trigger rule) requires modifying protocol handlers | Open/Closed principle. New behavior via new event handler, no existing code modified. | Phase 2 |
| **P2** | HA Integration | HA code scattered across Backend project with no connection to domain | Create `Adapters.HomeAssistant` project; define `IHomeAutomationGateway` in Application | HA integration will inevitably couple to OCPP handlers if not properly isolated | Clean anti-corruption layer between HA and core system | Phase 3 |
| **P2** | Persistence | `IOcppPersistence` is OCPP-branded with OCPP-specific DTOs | Replace with domain-level repository interfaces defined in Application | DB layer is structurally tied to OCPP. A second protocol can't use the same persistence path without OCPP-specific types. | Protocol-agnostic persistence. Proper DDD repositories. | Phase 3 |
| **P2** | Naming | Backend.OCPP `Domain/` folder contains protocol handlers, not domain logic | Rename to proper layer names; reserve "Domain" for actual domain project | Misleading structure leads developers to put domain logic in the wrong place | Honest architecture that guides correct placement of new code | Phase 3 |
| **P2** | Thread Safety | `ChargerStatus` objects mutated without locking (only the dictionary is concurrent) | Encapsulate mutations behind domain methods with proper synchronization or use immutable read model snapshots | Potential race conditions under concurrent protocol messages from multiple chargers | Correctness under load | Phase 2 |
| **P3** | Deployment | No Dockerfile, no HA Add-on config, hardcoded DB paths | Create Dockerfile, config.yaml, environment-based config | Cannot deploy to HA Add-on | Production deployment capability | Phase 4 |
| **P3** | Resilience | Startup fails if HA is unreachable | Make HA initialization resilient with retry/backoff | In Docker, HA might not be ready when the add-on starts | Reliable container startup | Phase 4 |
| **P3** | Observability | `Console.WriteLine` for diagnostics; no health checks | Structured logging + health check endpoints | Cannot monitor system health in production | Operational visibility | Phase 4 |
| **P3** | Testing | No test projects exist; god objects make unit testing near-impossible | Add test projects; domain and application layers designed for unit testing; adapters for integration testing | No regression safety net | Confidence in refactoring; catch bugs early | Phase 4 |

---

### Closing Notes

The codebase is at an early stage where these architectural changes are still tractable. The longer OCPP stays as the structural backbone, the more expensive the extraction becomes. Phase 1 (creating Domain and Application projects, decoupling controllers from OCPP types) can be done in a few days and immediately improves the extensibility posture. Phase 2 (real domain model, events) is the most impactful investment. Phases 3–4 are cleanup and deployment — necessary but less architecturally significant.

The single most impactful change is **task #11–18**: making `ChargePointSession` a thin OCPP translator that feeds domain commands, and moving all business decisions and side effects into the domain/application layers. Everything else flows from that decision. 

Completed: *Produce architectural review report* (7/7)



User: can you put all this into a memory file (.md file) so that I can copy it out of the chat

GitHub Copilot: Read memory

User: just create an .md file in the project root with everything you just described

GitHub Copilot: Create File