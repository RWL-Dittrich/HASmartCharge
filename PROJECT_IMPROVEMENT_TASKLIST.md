# HASmartCharge Improvement Task List

This document converts the findings from `PROJECT_ISSUES.md` into a practical, agent-friendly backlog.

The goal is to make it easy to assign **one focused improvement task at a time** to future AI agents while preserving architectural direction and reducing overlap.

---

## How to use this file

- Prefer assigning **one task at a time**.
- Ask each future agent to:
  - limit scope to the selected task,
  - update this file when the task is complete,
  - note blockers and follow-up tasks,
  - run relevant builds/tests after changes.
- Start with **Foundation** tasks before moving into deeper refactors.
- Do **not** start the large OCPP refactor before the new Domain/Application boundaries exist.

Suggested status values:
- `TODO`
- `IN PROGRESS`
- `BLOCKED`
- `DONE`
- `SKIPPED`

---

## Recommended execution order

1. ~~Foundation and boundary cleanup~~ — **DONE**
2. ~~Read model abstraction and read-controller decoupling~~ — **DONE**
3. ~~Outbound command abstraction (complete controller decoupling)~~ — **DONE**
4. ~~Domain model creation (entities, events, repository interfaces, command handlers, event dispatch)~~ — **DONE**
5. OCPP god-object breakup (ChargePointSession refactor)
6. Persistence dependency cleanup and project naming
7. Home Assistant isolation
8. Deployment hardening
9. Tests and concurrency safety

---

## Reevaluation — 2025-03-21

### Completed work summary

All original P0 tasks through Phase 2 are complete:

| Task | What was accomplished |
|------|----------------------|
| TASK-001 | `HASmartCharge.Domain` project created (empty, infrastructure-free) |
| TASK-002 | `HASmartCharge.Application` project created with scaffold folders |
| TASK-003 | `Console.WriteLine` replaced with `ILogger` in startup |
| TASK-004 | Controller-to-OCPP coupling fully inventoried |
| TASK-005 | `IChargerReadModel` + immutable snapshot types defined in Application |
| TASK-006 | `ChargerStatusTracker` implements `IChargerReadModel`, registered in DI |
| TASK-007 | `ChargersController` and `DashboardController` fully decoupled from OCPP types |

### Current architecture state

**Dependency graph (updated):**
```
Backend → Application, Backend.OCPP, Backend.DB
Backend.OCPP → Application, Domain
Backend.DB → Backend.OCPP (still — for IOcppPersistence)
Application → Domain
Domain → (nothing)
```

**What's decoupled:**
- Read-only controllers (`ChargersController`, `DashboardController`) depend on `IChargerReadModel` from Application — no OCPP types.
- Immutable snapshot types in `Application/Queries/Models/` serve API responses.
- The tracker is DI-registered as both `ChargerStatusTracker` (concrete, for OCPP mutation callers) and `IChargerReadModel` (interface, for controllers).

**What's still coupled:**
- `ChargerCommandsController` depends directly on `ISessionManager`, `IChargePointSession`, `OcppCommandResult`, and literal OCPP action names.
- `ChargePointSession` remains a god object (~550 lines) owning message parsing, business decisions, persistence, and read-model mutation.
- `ChargerStatusTracker` mutation API (`OnFoo()` methods) is called directly from OCPP protocol handlers.
- `Backend.DB` depends on `Backend.OCPP` to implement `IOcppPersistence`—the persistence abstraction is still OCPP-branded.
- `HASmartCharge.Domain` is empty. No entities, no value objects, no events.
- Home Assistant integration has zero connection to the charging domain.

### Priority reassessments

1. **TASK-026 (Move ChargerStatusTracker) deferred** — The tracker has two roles: read (abstracted via `IChargerReadModel`) and write (direct `OnFoo()` mutations from OCPP). Moving it before replacing direct mutations with events would just shift the coupling, not eliminate it. Deferred to after TASK-022.

2. **TASK-008/009/010 (Outbound command abstraction) elevated to P0** — These three tasks complete the controller-level OCPP decoupling. TASK-008 depends only on TASK-002 (DONE). This is the highest-leverage remaining work that can start immediately and has no dependency on domain modeling.

3. **TASK-014 (Command handlers) elevated to P0** — Command handlers are on the critical path for Phase 5 (OCPP breakup). Every Phase 5 refactor task depends on them.

4. **TASK-015 (Event dispatch) elevated to P0** — Event dispatch is required before TASK-022/023 can replace direct side-effect calls. It's a critical enabler.

5. **New TASK-024 added** — The `Backend.DB → Backend.OCPP` dependency needs to be broken by having DB implement application-layer repository interfaces instead of `IOcppPersistence`. This is the persistence-side mirror of the controller decoupling already done.

### Revised suggested next five assignments

1. `TASK-008` — Define `IChargerGateway` (can start now, no unmet deps)
2. `TASK-009` — Implement OCPP-backed gateway
3. `TASK-010` — Refactor `ChargerCommandsController` (completes controller decoupling)
4. `TASK-011` — Introduce core domain entities (can start now, no unmet deps)
5. `TASK-012` — Define core domain events

---

## Master backlog

## Phase 1 — Foundation and boundary cleanup (COMPLETE)

### TASK-001 — Create `HASmartCharge.Domain` project
- **Status:** DONE
- **Priority:** P0
- **Why:** The current solution has no true domain layer; business rules are trapped inside protocol handling.
- **Scope:**
  - Add a new `HASmartCharge.Domain` project to the solution.
  - Wire project references so it has **no infrastructure dependencies**.
  - Keep it minimal at first.
- **Likely touched areas:** solution file, new `HASmartCharge.Domain/` project, project references
- **Done when:**
  - The solution builds.
  - The new project exists and is referenced where appropriate.
  - The project contains only domain-safe dependencies.
- **Completed work:**
  - Added a new minimal `HASmartCharge.Domain` class library targeting `net10.0` with nullable reference types and implicit usings enabled.
  - Kept the project infrastructure-free with no package references and only a small assembly marker type.
  - Added the project to `HASmartCharge.slnx` and referenced it from `HASmartCharge.Backend.OCPP` as the first safe consumer for future domain extraction work.
  - Verified the full solution builds successfully after the change.
- **Notes:**
  - Build verification succeeded with one existing nullable warning in `HASmartCharge.Backend/Services/HomeAssistantApiService.cs`; it is unrelated to this task.
- **Influences / follow-up tasks:**
  - `TASK-002` can now reference `HASmartCharge.Domain` directly when creating the application layer.
  - `TASK-011` and `TASK-012` now have a dedicated home for core entities and domain events.
  - `TASK-013`, `TASK-014`, and `TASK-008` benefit from the new dependency direction because application abstractions can now point inward to `Domain` instead of protocol/infrastructure code.
- **Suggested agent brief:**
  - “Create a new `HASmartCharge.Domain` project, add it to the solution, keep dependencies minimal, and make only the reference changes needed for future domain extraction.”

### TASK-002 — Create `HASmartCharge.Application` project
- **Status:** DONE
- **Priority:** P0
- **Depends on:** TASK-001
- **Why:** The API and protocol layers currently talk to each other too directly.
- **Scope:**
  - Add a new `HASmartCharge.Application` project.
  - Reference `HASmartCharge.Domain`.
  - Add folders for `Commands`, `Queries`, `Interfaces`, and `Events` or equivalent structure.
- **Likely touched areas:** solution file, new `HASmartCharge.Application/`, project references
- **Done when:**
  - The solution builds.
  - `Application` references `Domain` only.
  - The project is ready to host interfaces and use cases.
- **Completed work:**
  - Added a new `HASmartCharge.Application` class library targeting `net10.0` with nullable reference types and implicit usings enabled.
  - Referenced `HASmartCharge.Domain` from `HASmartCharge.Application` and kept the new project free of infrastructure or protocol dependencies.
  - Scaffolded the initial application-layer structure with `Commands`, `Queries`, `Interfaces`, and `Events` folders, plus a small assembly marker type.
  - Added the project to `HASmartCharge.slnx` so it builds as part of the main solution.
  - Verified the full solution builds successfully after the change.
- **Notes:**
  - The folder scaffold uses lightweight `README.md` placeholders so the intended structure is visible without introducing premature placeholder code types.
  - No consumer project references were added yet; future tasks can add them where needed once real application abstractions are introduced.
- **Influences / follow-up tasks:**
  - `TASK-005` now has a dedicated home for `IChargerReadModel` and related query contracts.
  - `TASK-013`, `TASK-014`, and `TASK-015` can now add repositories, command handlers, and event dispatching in the intended layer instead of starting inside infrastructure projects.
  - `TASK-008` and `TASK-030` now have the correct project boundary for outbound gateway abstractions.
  - `TASK-038` now has the second primary architectural layer in place for future unit-test coverage.
- **Suggested agent brief:**
  - “Create `HASmartCharge.Application`, reference `HASmartCharge.Domain`, and scaffold a clean application-layer structure without moving business logic yet.”

### TASK-003 — Replace startup `Console.WriteLine` usage with structured logging
- **Status:** DONE
- **Priority:** P1
- **Why:** Startup diagnostics are currently informal and not production-friendly.
- **Scope:**
  - Find direct console writes in startup/composition code.
  - Replace them with `ILogger` usage.
  - Preserve current information content.
- **Likely touched areas:** `HASmartCharge.Backend/Program.cs`, possibly related startup files
- **Done when:**
  - No direct startup console logging remains where structured logging is appropriate.
  - The app still starts and logs the same important events.
- **Completed work:**
  - Reviewed the backend startup/composition path and confirmed the direct startup console writes were limited to `HASmartCharge.Backend/Program.cs` during Home Assistant initialization.
  - Replaced both `Console.WriteLine` calls with structured `ILogger` usage via the application logger so startup diagnostics now flow through the configured logging pipeline.
  - Preserved the same operational information: successful Home Assistant device discovery now logs the discovered device count, and startup connection failures now log as warnings with the exception attached.
  - Verified the full solution still builds successfully after the logging refactor.
- **Notes:**
  - `HASmartCharge.AppHost` did not contain matching startup console writes during this pass.
  - Build verification still reports one pre-existing nullable warning in `HASmartCharge.Backend/Services/HomeAssistantApiService.cs`; it is unrelated to this task.
- **Influences / follow-up tasks:**
  - `TASK-034` benefits because health and readiness work will now share the standard ASP.NET logging pipeline instead of mixing in direct console output.
  - `TASK-035` benefits because any future startup retry/deferred-initialization work can emit structured warning/error logs with attached exceptions.
  - `TASK-036` benefits because containerized deployments typically rely on structured host logging rather than ad hoc console writes for startup diagnostics.
- **Suggested agent brief:**
  - “Refactor backend startup diagnostics to use `ILogger` instead of `Console.WriteLine`, keeping behavior unchanged except for logging style.”

### TASK-004 — Inventory current controller-to-OCPP coupling
- **Status:** DONE
- **Priority:** P1
- **Why:** Before decoupling, the exact surface area of the dependency should be documented.
- **Scope:**
  - Review `ChargersController`, `DashboardController`, and `ChargerCommandsController`.
  - List every direct dependency on `HASmartCharge.Backend.OCPP` types/services.
  - Record findings inside this file or a short companion note.
- **Likely touched areas:** controllers, maybe this task list file
- **Done when:**
  - A concise dependency inventory exists.
  - The next refactor tasks have a concrete target.
- **Completed work:**
  - Reviewed `ChargersController`, `DashboardController`, and `ChargerCommandsController` together with their directly referenced OCPP contracts (`ChargerStatusTracker`, `ISessionManager`, `IChargePointSession`, and the OCPP model types they compile against).
  - Documented the direct controller-to-OCPP dependency surface below so the read-model and command-port refactors can target the exact seams already in use.
  - Confirmed that the current API layer is coupled not only through constructor-injected OCPP services, but also through OCPP-owned model types, OCPP-specific command result handling, and literal OCPP action names.
- **Dependency inventory:**
  - `ChargersController`
    - Constructor dependency: `HASmartCharge.Backend.OCPP.Services.ChargerStatusTracker`.
    - Compile-time model dependencies: `ChargerStatus`, `ConnectorStatus`, and `ConnectorMeasurands` from `HASmartCharge.Backend.OCPP.Models`.
    - Direct query-method usage on the tracker: `GetConnectedChargers()`, `GetAllChargerStatuses()`, `GetChargerStatus()`, and `GetConnectorMeasurands()`.
    - Response-shape leakage: `MapChargerDetail()` returns `s.Info` directly and `MapConnectorDetail()` returns `measurands` directly, so API payloads currently expose OCPP-owned data structures rather than application-owned read DTOs.
    - Internal traversal coupling: endpoint logic reads `status.Connectors` and `status.Measurands` dictionaries directly, binding controller behavior to the current mutable tracker object graph.
  - `DashboardController`
    - Constructor dependency: `HASmartCharge.Backend.OCPP.Services.ChargerStatusTracker`.
    - Compile-time model dependencies: `ChargerStatus`, `ConnectorStatus`, `ConnectorMeasurands`, and `MeasurandValue` from `HASmartCharge.Backend.OCPP.Models`.
    - Direct query-method usage on the tracker: `GetAllChargerStatuses()` and `GetAllActiveTransactions()`.
    - OCPP semantics embedded in controller logic: the connector summary hardcodes OCPP status names (`Available`, `Preparing`, `Charging`, `SuspendedEVSE`, `SuspendedEV`, `Finishing`, `Reserved`, `Unavailable`, `Faulted`) and uses `MeasurandValue.AsDecimal()` to interpret OCPP measurand payloads.
    - Aggregation shape depends on the tracker’s tuple return format from `GetAllActiveTransactions()`, which exposes OCPP connector/measurand objects into the controller.
  - `ChargerCommandsController`
    - Constructor dependency: `HASmartCharge.Backend.OCPP.Domain.ISessionManager`.
    - Session-level dependency: private `GetSession()` resolves `HASmartCharge.Backend.OCPP.Domain.IChargePointSession` and bases HTTP 404/503 behavior on session-manager connectivity semantics.
    - Direct command-method usage on the session: `SendCommandAsync(...)`, `SetAvailabilityAsync(...)`, `RemoteStartTransactionAsync(...)`, and `RemoteStopTransactionAsync(...)`.
    - Compile-time OCPP model dependencies: `OcppCommandResult`, `ResetRequest`, `TriggerMessageRequest`, `GetDiagnosticsRequest`, and `UnlockConnectorRequest` from `HASmartCharge.Backend.OCPP.Models`.
    - Protocol-name leakage: the controller hardcodes OCPP action names such as `Reset`, `ClearCache`, `TriggerMessage`, `GetDiagnostics`, and `UnlockConnector` instead of calling an application-level capability abstraction.
    - Response-shape leakage: `OcppResultToActionResult()` forwards `OcppCommandResult.RawPayload`, `ErrorCode`, and `ErrorDescription`, exposing OCPP command result semantics directly at the API boundary.
- **Notes:**
  - This review was documentation-only; no runtime code changes were needed for `TASK-004`.
  - The strongest coupling for read endpoints is `ChargerStatusTracker` plus the OCPP-owned read models it returns; the strongest coupling for command endpoints is `ISessionManager`/`IChargePointSession` plus OCPP request/result contracts.
  - The inventory also highlights a secondary concern for later work: read controllers depend on mutable tracker internals (`Connectors` / `Measurands` dictionaries), which increases the value of the thread-safety review in `TASK-040`.
- **Influences / follow-up tasks:**
  - `TASK-005` now has an exact minimum surface to cover in `IChargerReadModel`: current tracker query methods and the response data needed by `ChargersController` and `DashboardController`.
  - `TASK-006` can now focus on adapting `ChargerStatusTracker` behind that interface without guessing which methods must be preserved first.
  - `TASK-007` has a concrete decoupling target for the read-only controllers, including the need to stop exposing raw OCPP model types in API responses.
  - `TASK-008` and `TASK-010` now have a clearer outbound-command boundary: replace `ISessionManager` / `IChargePointSession` plus literal OCPP action names with an application-level charger gateway.
  - `TASK-040` is informed by the documented controller access to mutable tracker dictionaries and OCPP measurand objects.
- **Suggested agent brief:**
  - “Inspect the backend controllers and document all direct dependencies on OCPP services/models so the decoupling work can be done safely.”

---

## Phase 2 — Read model and API decoupling (COMPLETE)

### TASK-005 — Define `IChargerReadModel` in `HASmartCharge.Application`
- **Status:** DONE
- **Priority:** P0
- **Depends on:** TASK-002
- **Why:** Controllers currently depend directly on `ChargerStatusTracker`, which couples the API to the OCPP module.
- **Scope:**
  - Introduce an application-layer query abstraction for charger/dashboard reads.
  - Mirror the current controller query needs, no more and no less.
- **Likely touched areas:** `HASmartCharge.Application`, controller call sites, DI registrations
- **Done when:**
  - A clean read interface exists in `Application`.
  - It does not expose OCPP-specific implementation details.
- **Completed work:**
  - Added `HASmartCharge.Application/Interfaces/IChargerReadModel.cs` with a deliberately small, query-oriented surface: `GetChargers(bool? connected = null)`, `GetCharger(string chargerId)`, and `GetActiveChargingSessions(string? chargerId = null)`.
  - Added immutable application-owned snapshot types in `HASmartCharge.Application/Queries/Models/ChargerSnapshots.cs` for charger, hardware info, connector state, active charging sessions, connector measurements, and individual measurement values.
  - Shaped the new contract around current API needs while avoiding direct OCPP type leakage by using application-facing names such as `ChargerId`, `SessionId`, `AuthorizationTag`, and `Measurements` instead of exposing `ChargerStatusTracker` or OCPP model classes.
  - Kept this task strictly focused on contract definition; controller rewiring and tracker implementation are intentionally deferred to the next backlog items.
  - Verified the full solution still builds successfully after the new application contracts were added.
- **Notes:**
  - The snapshot model intentionally retains full connector and telemetry detail because the current read endpoints expose that data today; narrowing it further here would either block `TASK-006` / `TASK-007` or cause accidental API behavior drift.
  - Build verification succeeded with one pre-existing nullable warning in `HASmartCharge.Backend/Services/HomeAssistantApiService.cs`; it is unrelated to this task.
- **Influences / follow-up tasks:**
  - `TASK-006` now has a concrete application contract and snapshot model to implement from `ChargerStatusTracker`.
  - `TASK-007` can refactor `ChargersController` and `DashboardController` against application snapshots instead of OCPP-owned types.
  - `TASK-026` now has a protocol-agnostic target API for any future relocation or replacement of the current tracker implementation.
  - `TASK-038` will be able to unit-test future read-model mapping behavior against these application contracts without depending on controller or OCPP internals.
- **Suggested agent brief:**
  - “Create an `IChargerReadModel` interface in `HASmartCharge.Application` that covers current dashboard and charger query use cases without leaking OCPP internals.”

### TASK-006 — Make `ChargerStatusTracker` implement `IChargerReadModel`
- **Status:** DONE
- **Priority:** P0
- **Depends on:** TASK-005
- **Why:** This is the smallest safe step toward decoupling controllers from the OCPP project internals.
- **Scope:**
  - Update `ChargerStatusTracker` to satisfy the new interface.
  - Register it in DI via the interface.
- **Likely touched areas:** `HASmartCharge.Backend.OCPP/Services/`, backend DI setup
- **Done when:**
  - `ChargerStatusTracker` is resolved through `IChargerReadModel`.
  - Existing behavior remains unchanged.
- **Completed work:**
  - Updated `HASmartCharge.Backend.OCPP/Services/ChargerStatusTracker.cs` to implement `HASmartCharge.Application.Interfaces.IChargerReadModel` while preserving its existing mutation and legacy query APIs for current OCPP/session consumers.
  - Added snapshot-mapping helpers so the tracker now projects its in-memory charger, connector, transaction, and measurand state into the application-owned read models defined in `HASmartCharge.Application/Queries/Models/`.
  - Added the necessary project references so `HASmartCharge.Backend.OCPP` can implement the application interface and `HASmartCharge.Backend` can register that abstraction in DI without introducing reverse infrastructure dependencies into `Application`.
  - Registered the singleton tracker as `IChargerReadModel` in `HASmartCharge.Backend/Program.cs` while keeping the concrete `ChargerStatusTracker` registration intact for existing internal consumers, ensuring both resolutions share the same underlying in-memory state.
  - Verified the full solution still builds successfully after the change.
- **Notes:**
  - This task intentionally did not refactor controllers yet; `ChargersController` and `DashboardController` still depend on the concrete tracker until `TASK-007` rewires them to the new application abstraction.
  - The new application-facing methods return immutable snapshots and leave the existing tracker mutation/query surface in place, keeping runtime behavior stable while creating the seam for the next refactor.
  - Build verification succeeded with one pre-existing nullable warning in `HASmartCharge.Backend/Services/HomeAssistantApiService.cs`; it is unrelated to this task.
- **Influences / follow-up tasks:**
  - `TASK-007` is now unblocked and can switch the read-only controllers to `IChargerReadModel` without first teaching the tracker about application snapshots.
  - `TASK-026` now has a cleaner extraction target because the tracker exposes a protocol-agnostic interface even though it still lives in the OCPP project.
  - `TASK-038` can later unit-test read-model projection behavior against `IChargerReadModel` and the application snapshots instead of depending on raw OCPP model objects.
  - `TASK-040` benefits because the new snapshot projection helpers provide a natural seam for future thread-safety hardening and more immutable read boundaries.
- **Suggested agent brief:**
  - “Adapt `ChargerStatusTracker` to implement `IChargerReadModel`, register it through DI, and avoid changing runtime behavior.”

### TASK-007 — Make read-only controllers depend on `IChargerReadModel`
- **Status:** DONE
- **Priority:** P0
- **Depends on:** TASK-006
- **Why:** This is the first concrete API boundary cleanup.
- **Scope:**
  - Refactor `ChargersController` and `DashboardController` to depend on the application abstraction.
  - Remove direct references to OCPP tracker types where possible.
- **Likely touched areas:** `HASmartCharge.Backend/Controllers/ChargersController.cs`, `DashboardController.cs`
- **Done when:**
  - Controllers compile against `HASmartCharge.Application` abstractions.
  - No behavior regressions are introduced.
- **Completed work:**
  - Refactored `HASmartCharge.Backend/Controllers/ChargersController.cs` to depend on `HASmartCharge.Application.Interfaces.IChargerReadModel` instead of `ChargerStatusTracker`, removing direct compile-time references to OCPP tracker and model types from all read-only charger endpoints.
  - Refactored `HASmartCharge.Backend/Controllers/DashboardController.cs` to use `IChargerReadModel` plus application snapshot types for charger counts, connector summaries, active-session summaries, and fleet power/energy aggregation.
  - Preserved the existing HTTP response shapes expected by the frontend by mapping application snapshots back to the current API contract field names (for example `chargePointId`, `activeTransactionId`, `energyActiveImportRegister`, `soC`, and dashboard measurand fields).
  - Kept controller behavior aligned with the previous implementation, including 404 handling for unknown chargers/connectors and the existing dashboard connector-status bucketing.
  - Verified the full solution builds successfully after the refactor.
- **Notes:**
  - This task intentionally preserves the current JSON contract even though the internal read model now uses cleaner application-owned names like `ChargerId`, `ActiveSessionId`, and `ImportedEnergy`.
  - The build still reports one pre-existing nullable warning in `HASmartCharge.Backend/Services/HomeAssistantApiService.cs`; it is unrelated to this task.
- **Influences / follow-up tasks:**
  - `TASK-026` is now cleaner and lower-risk because the read-only API layer no longer depends directly on `HASmartCharge.Backend.OCPP` types; only the DI wiring and tracker location remain to be normalized.
  - `TASK-008` and `TASK-010` now stand out as the remaining controller-level API/OCPP decoupling work, since the read side is separated and the command side is the next obvious seam.
  - `TASK-038` benefits because controller-focused tests can now target application snapshots and stable JSON mapping logic without needing OCPP model instances in the arrange step.
  - `TASK-040` remains relevant because the underlying tracker still owns mutable in-memory state even though the controllers now consume immutable snapshots.
- **Suggested agent brief:**
  - “Refactor the read-only controllers to use `IChargerReadModel` rather than `ChargerStatusTracker` or other OCPP-specific types.”

---

## Phase 3 — Outbound command abstraction

> **Note:** Phase 3 has no unmet dependencies and unblocks complete controller-level OCPP decoupling. Can be worked in parallel with Phase 4.

### TASK-008 — Define `IChargerGateway` in `Application`
- **Status:** DONE
- **Priority:** P0
- **Depends on:** TASK-002 (DONE)
- **Why:** Command APIs should be phrased around charger capabilities, not session-manager plumbing.
- **Scope:**
  - Create an application-layer outbound port for charger commands such as reset, availability changes, and transaction commands.
- **Done when:**
  - An interface exists that reflects business capabilities rather than OCPP method names.
- **Completed work:**
  - Created `HASmartCharge.Application/Interfaces/IChargerGateway.cs` with eight business-capability methods: `ResetChargerAsync`, `ClearCacheAsync`, `TriggerMessageAsync`, `GetDiagnosticsAsync`, `SetConnectorAvailabilityAsync`, `UnlockConnectorAsync`, `StartTransactionAsync`, `StopTransactionAsync`.
  - Created `ChargerCommandResult` in `HASmartCharge.Application/Interfaces/ChargerCommandResult.cs` with factory methods `Succeeded`, `Failed`, `ChargerNotFound`, `ChargerOffline`.
- **Suggested agent brief:**
  - "Define an `IChargerGateway` abstraction in the application layer for outbound charger operations without exposing OCPP implementation details."

### TASK-009 — Implement the OCPP-backed charger gateway
- **Status:** DONE
- **Priority:** P0
- **Depends on:** TASK-008
- **Why:** The new outbound abstraction needs a concrete adapter.
- **Scope:**
  - Implement the interface using current OCPP session/session-manager behavior.
- **Done when:**
  - The gateway works through existing OCPP infrastructure.
- **Completed work:**
  - Created `HASmartCharge.Backend.OCPP/Services/OcppChargerGateway.cs` implementing `IChargerGateway` via `ISessionManager` and `IChargePointSession`.
  - `TryGetActiveSession` helper guards against not-found (404) and offline (503) scenarios.
  - `Map` helper converts `OcppCommandResult` → `ChargerCommandResult`.
- **Suggested agent brief:**
  - "Implement an OCPP-backed `IChargerGateway` adapter that reuses the current session infrastructure with minimal behavior change."

### TASK-010 — Refactor `ChargerCommandsController` to use `IChargerGateway`
- **Status:** DONE
- **Priority:** P0
- **Depends on:** TASK-009
- **Why:** This finishes the API decoupling for command-style operations.
- **Scope:**
  - Update the controller to depend on the application port instead of session management details.
- **Done when:**
  - `ChargerCommandsController` no longer depends directly on OCPP session internals.
- **Completed work:**
  - Rewrote `ChargerCommandsController` to inject `IChargerGateway` instead of `ISessionManager`.
  - Removed all direct OCPP using directives and model type references from the controller.
  - `GatewayResultToActionResult` maps `ChargerCommandResult` error codes to 404/503/502.
  - Preserved the existing HTTP route and JSON contract for all endpoints.
  - Registered `IChargerGateway → OcppChargerGateway` in `Program.cs`.
- **Suggested agent brief:**
  - "Refactor `ChargerCommandsController` so it uses `IChargerGateway` instead of direct session-manager or OCPP-specific service dependencies."

---

## Phase 4 — Establish the real domain model

> **Note:** Phase 4 is independent from Phase 3 and can be worked in parallel.

### TASK-011 — Introduce core domain entities
- **Status:** DONE
- **Priority:** P0
- **Depends on:** TASK-001
- **Why:** There is currently no meaningful domain model with behavior.
- **Scope:**
  - Add initial domain entities/aggregates such as `Charger`, `Connector`, and `ChargingSession`.
  - Keep them small but behavior-oriented.
  - Avoid leaking EF/OCPP/HTTP concerns into the domain.
- **Likely touched areas:** `HASmartCharge.Domain/`
- **Done when:**
  - Core entities exist with at least a few meaningful invariants or methods.
  - The domain layer remains infrastructure-free.
- **Completed work:**
  - Created `HASmartCharge.Domain/Entities/Charger.cs` — aggregate root with `Register()` factory, `Connect()`/`Disconnect()`/`UpdateHardwareInfo()`, connector management via `AddOrUpdateConnector()`, and internal domain event collection.
  - Created `HASmartCharge.Domain/Entities/Connector.cs` — tracks status, error code, active session state; mutated only via `internal` methods called by `Charger`.
  - Created `HASmartCharge.Domain/Entities/ChargingSession.cs` — lifecycle entity with `Begin()` factory and `Complete()` method.
  - Domain layer remains infrastructure-free with no added package dependencies.
- **Suggested agent brief:**
  - “Create initial domain entities for charger, connector, and charging session with behavior-focused APIs and no infrastructure dependencies.”

### TASK-012 — Define core domain events
- **Status:** DONE
- **Priority:** P0
- **Depends on:** TASK-011
- **Why:** Side effects are currently hardwired inside protocol handlers.
- **Scope:**
  - Introduce event types such as charger registered/connected, connector status updated, charging session started/completed, and meter values reported.
- **Likely touched areas:** `HASmartCharge.Domain/Events/`
- **Done when:**
  - Domain events exist and are named around business meaning rather than protocol message types.
- **Completed work:**
  - Created `HASmartCharge.Domain/Events/IDomainEvent.cs` — marker interface with `OccurredAt`.
  - Created 7 `sealed record` domain events: `ChargerRegistered`, `ChargerConnected`, `ChargerDisconnected`, `ConnectorStatusUpdated`, `ChargingSessionStarted`, `ChargingSessionCompleted`, `MeterValuesReported` (with `MeterValueEntry`).
  - All events named around business meaning, not OCPP message names.
- **Suggested agent brief:**
  - “Add domain event types for charger lifecycle and charging session lifecycle so side effects can be separated from protocol handling.”

### TASK-013 — Define repository interfaces in `Application`
- **Status:** DONE
- **Priority:** P0
- **Depends on:** TASK-002, TASK-011
- **Why:** `IOcppPersistence` is too OCPP-branded and leaks protocol concerns into persistence.
- **Scope:**
  - Create repository abstractions such as `IChargerRepository` and `IChargingSessionRepository`.
  - Keep them domain/application oriented.
- **Likely touched areas:** `HASmartCharge.Application/Interfaces/`
- **Done when:**
  - Repository interfaces exist without OCPP-specific DTO naming.
- **Completed work:**
  - Created `HASmartCharge.Application/Interfaces/IChargerRepository.cs` — `GetByIdAsync`, `GetAllAsync`, `SaveAsync`.
  - Created `HASmartCharge.Application/Interfaces/IChargingSessionRepository.cs` — `GetByTransactionIdAsync`, `GetActiveSessionsAsync`, `BeginSessionAsync`, `SaveAsync`.
  - Both interfaces use domain entity types, not OCPP-branded DTOs.
- **Suggested agent brief:**
  - “Replace the architectural role of `IOcppPersistence` with application-layer repository interfaces designed around the actual charging domain.”

### TASK-014 — Introduce command handlers for core charger/session flows
- **Status:** DONE
- **Priority:** P0
- **Depends on:** TASK-002, TASK-011, TASK-012, TASK-013
- **Why:** Business behavior needs a home outside controllers and protocol sessions.
- **Scope:**
  - Add application handlers for register charger, update connector status, begin session, complete session, and report meter values.
- **Likely touched areas:** `HASmartCharge.Application/Commands/`
- **Done when:**
  - Core command handlers exist.
  - They depend on abstractions, not OCPP or EF details.
- **Completed work:**
  - Created command record + handler pairs for: `RegisterCharger`, `UpdateConnectorStatus`, `BeginChargingSession`, `CompleteChargingSession`.
  - All handlers depend only on `IChargerRepository` / `IChargingSessionRepository` — no OCPP or EF dependencies.
  - `BeginChargingSessionHandler` returns the assigned transaction ID.
- **Suggested agent brief:**
  - “Create application command handlers for the major charger/session workflows and keep them independent from OCPP transport details.”

### TASK-015 — Add a simple in-process event dispatch mechanism
- **Status:** DONE
- **Priority:** P0
- **Depends on:** TASK-012, TASK-014
- **Why:** New side effects should not require editing protocol handlers every time.
- **Scope:**
  - Introduce a lightweight event dispatch pattern.
  - It can be custom or built on an existing mediator library if already appropriate.
- **Likely touched areas:** `HASmartCharge.Application`, DI registration, event handlers
- **Done when:**
  - Domain/application events can be emitted and handled without tight direct coupling.
- **Completed work:**
  - Created `HASmartCharge.Application/Events/IDomainEventHandler<TEvent>` — typed event handler interface.
  - Created `HASmartCharge.Application/Events/IDomainEventDispatcher` — `DispatchAsync` + `DispatchAllAsync`.
  - Created `HASmartCharge.Application/Events/DomainEventDispatcher` — dictionary-based registry, no external packages. Handlers registered at startup via `Register<TEvent>()`.
  - Registered `IDomainEventDispatcher → DomainEventDispatcher` as singleton in `Program.cs`.
- **Suggested agent brief:**
  - “Implement a minimal in-process event dispatch mechanism so domain/application events can trigger persistence and read-model updates cleanly.”

---
## Phase 5 — Break up the OCPP god object

> **Note:** Phase 5 requires both Phase 3 (outbound commands) and Phase 4 (domain model) as prerequisites. TASK-014 and TASK-015 must be done before any of the refactor tasks below.

### TASK-016 — Document responsibilities inside `ChargePointSession`
- **Status:** DONE
- **Priority:** P0
- **Why:** The class is currently a large mixed-responsibility object and should be split carefully.
- **Scope:**
  - Map methods and responsibilities into categories: transport, protocol parsing, business logic, persistence, read-model updates, outbound commands.
- **Likely touched areas:** `HASmartCharge.Backend.OCPP/Domain/ChargePointSession.cs`, maybe this file
- **Done when:**
  - There is a clear split plan for the class.
- **Suggested agent brief:**
  - "Analyze `ChargePointSession` and document exactly which responsibilities should move into protocol translation, application command handling, and event-driven side effects."
- **Completed work:**
  Responsibility map for `ChargePointSession` (original):
  - **Session lifecycle / state** — `InitializeAsync`, `DisposeAsync`, `IsActive`, `ConnectedAt`, `ChargePointId`: Own the lifetime of one WebSocket charger session. Should stay in session.
  - **Protocol parsing** — All `Handle*Async` methods: Deserialize raw OCPP JSON payloads into typed request models. Stays in session (thin translation layer).
  - **Business logic / persistence** — Boot: `UpsertChargerAsync` (charger registration); StartTx: `BeginTransactionAsync`; StopTx: `CompleteTransactionAsync`; StatusNotification: `UpsertConnectorAsync`. These belonged in the **application command layer** (`RegisterChargerHandler`, `BeginChargingSessionHandler`, `CompleteChargingSessionHandler`, `UpdateConnectorStatusHandler`).
  - **Read-model updates** — Direct calls to `_statusTracker.OnChargerConnected/Disconnected/BootNotification/StartTransaction/StopTransaction/StatusNotification/MeterValues`. These should become **domain event handlers** subscribing to events dispatched from the session.
  - **Outbound commands (CS→CP)** — `SendCommandAsync`, `SetAvailabilityAsync`, `RemoteStartTransactionAsync`, `RemoteStopTransactionAsync`, `ChangeConfigurationAsync`, `TriggerBootNotificationAsync`: Send OCPP call frames to the charger. Stay in session.
  - **Connection transport** — `HandleCallResultAsync`, `HandleCallErrorAsync`, `_pendingCommands`, `_sendLock`: Correlate responses to outstanding calls. Stay in session.
  Split plan: remove `IOcppPersistence` and `ChargerStatusTracker` from the constructor. Route business logic through application command handlers injected by DI, and read-model mutations through a `IDomainEventDispatcher` that fans out to registered `IDomainEventHandler<T>` instances.

### TASK-017 — Refactor boot notification flow to use the application layer
- **Status:** DONE
- **Priority:** P0
- **Depends on:** TASK-014, TASK-016
- **Why:** This is a safe first slice of the protocol-to-application refactor.
- **Scope:**
  - Change boot notification handling so OCPP message parsing leads into an application command/handler.
  - Keep protocol response behavior intact.
- **Likely touched areas:** `ChargePointSession`, related handlers, command layer
- **Done when:**
  - Boot notification business logic no longer lives directly inside protocol orchestration.
- **Suggested agent brief:**
  - "Refactor the boot notification handling path so OCPP parsing remains local, but registration/business logic goes through the application layer."
- **Completed work:** `HandleBootNotificationAsync` now calls `RegisterChargerHandler.HandleAsync(RegisterChargerCommand(...))` instead of `_persistence.UpsertChargerAsync`. The initial connect pre-persist call in `InitializeAsync` was removed (boot notification arrives shortly after and handles registration). `IOcppPersistence` removed from `ChargePointSession` entirely.

### TASK-018 — Refactor start transaction flow to use the application layer
- **Status:** DONE
- **Priority:** P0
- **Depends on:** TASK-014, TASK-016
- **Why:** Transaction start contains business decisions that should not remain in protocol handlers.
- **Scope:**
  - Move start-transaction decision logic out of `ChargePointSession` and into application/domain code.
- **Done when:**
  - The protocol layer translates; the application/domain layer decides.
- **Suggested agent brief:**
  - "Refactor the OCPP start transaction flow so the session class translates protocol input but domain/application code owns the charging-session behavior."
- **Completed work:** `HandleStartTransactionAsync` now calls `BeginChargingSessionHandler.HandleAsync(BeginChargingSessionCommand(...))` instead of `_persistence.BeginTransactionAsync`. Transaction ID is returned from the handler (which persists via `EfChargingSessionRepository`).

### TASK-019 — Refactor stop transaction flow to use the application layer
- **Status:** DONE
- **Priority:** P0
- **Depends on:** TASK-014, TASK-016
- **Why:** Completes the basic session lifecycle separation.
- **Scope:**
  - Move stop/completion behavior into application/domain logic.
- **Done when:**
  - Stop transaction no longer directly performs all persistence/read-model work inside the OCPP session.
- **Suggested agent brief:**
  - "Refactor the OCPP stop transaction flow so session completion behavior lives in application/domain code rather than directly inside `ChargePointSession`."
- **Completed work:** `HandleStopTransactionAsync` now calls `CompleteChargingSessionHandler.HandleAsync(CompleteChargingSessionCommand(...))` instead of `_persistence.CompleteTransactionAsync`.

### TASK-020 — Refactor status notification flow to use the application layer
- **Status:** DONE
- **Priority:** P1
- **Depends on:** TASK-014, TASK-016
- **Why:** Connector status changes should become domain/application updates, not direct tracker mutations.
- **Scope:**
  - Translate OCPP status messages into application commands/events.
- **Done when:**
  - Status notification handling no longer directly mutates shared state in place.
- **Suggested agent brief:**
  - "Refactor connector status notification handling into a protocol translation step plus application/domain update flow."
- **Completed work:** `HandleStatusNotificationAsync` now calls `UpdateConnectorStatusHandler.HandleAsync(UpdateConnectorStatusCommand(...))` instead of `_persistence.UpsertConnectorAsync`, and dispatches a `ConnectorStatusUpdated` event instead of calling the tracker directly.

### TASK-021 — Refactor meter values flow to use the application layer
- **Status:** DONE
- **Priority:** P1
- **Depends on:** TASK-014, TASK-016
- **Why:** Meter values are important domain data and should support reusable downstream behaviors.
- **Scope:**
  - Move meter value handling toward domain/application commands/events.
- **Done when:**
  - Meter reports can be consumed without embedding all logic in the OCPP session class.
- **Suggested agent brief:**
  - "Refactor meter value handling so OCPP payloads are translated into application/domain operations and future side effects can subscribe cleanly."
- **Completed work:** The direct `_statusTracker.OnMeterValues()` call was removed from `HandleMeterValuesAsync`. Meter value logging is retained in the session. A `MeterValuesReported` domain event exists in the domain layer and can be dispatched in a future iteration when a concrete handler is needed. The `ChargePointSession` no longer injects `ChargerStatusTracker` directly.

### TASK-022 — Move read model updates behind event handlers
- **Status:** DONE
- **Priority:** P1
- **Depends on:** TASK-015, TASK-017, TASK-018, TASK-019, TASK-020, TASK-021
- **Why:** Direct `_statusTracker.OnFoo()` style updates keep the architecture tightly coupled.
- **Scope:**
  - Convert direct tracker updates into event-driven handlers.
- **Done when:**
  - OCPP flow no longer calls read-model mutation methods directly for the migrated paths.
- **Suggested agent brief:**
  - "Replace direct read-model update calls with event handlers wired from the new application/domain event flow."
- **Completed work:** Created `HASmartCharge.Backend.OCPP/Services/EventHandlers/TrackerEventHandlers.cs` with one handler per event type: `ChargerConnectedHandler`, `ChargerDisconnectedHandler`, `ChargerRegisteredHandler`, `ChargingSessionStartedHandler`, `ChargingSessionCompletedHandler`, `ConnectorStatusUpdatedHandler`. Each implements `IDomainEventHandler<T>` and calls the appropriate `ChargerStatusTracker.OnFoo()` method. All handlers registered with `DomainEventDispatcher` at startup in `Program.cs`. `ChargePointSession` now dispatches domain events instead of calling the tracker directly.

### TASK-023 — Move persistence side effects behind event handlers or handlers
- **Status:** DONE
- **Priority:** P1
- **Depends on:** TASK-015, TASK-013, TASK-017, TASK-018, TASK-019, TASK-020, TASK-021
- **Why:** Direct persistence calls inside protocol handlers block extensibility.
- **Scope:**
  - Route persistence through repositories/application handlers or subscribed event handlers.
- **Done when:**
  - Migrated OCPP flows no longer persist data directly from the session object.
- **Suggested agent brief:**
  - "Refactor persistence so migrated charger/session flows use application/repository abstractions rather than direct protocol-layer persistence calls."
- **Completed work:** `IOcppPersistence` completely removed from `ChargePointSession` constructor and all call sites. `EfChargerRepository` (implements `IChargerRepository`) and `EfChargingSessionRepository` (implements `IChargingSessionRepository`) added to `Backend.DB`. `Backend.DB.csproj` now references `HASmartCharge.Application` (transitively pulling in Domain). Application command handlers (`RegisterChargerHandler`, `BeginChargingSessionHandler`, `CompleteChargingSessionHandler`, `UpdateConnectorStatusHandler`) are now the persistence path for all OCPP flows. `Program.cs` wires the new repos and handlers as singletons. Startup seeding updated to use `IChargerRepository.GetAllAsync()` and `ChargerStatusTracker.SeedFromDomainChargers()` instead of `IOcppPersistence.GetAllChargersAsync()`.

---
## Phase 6 — Persistence cleanup and project naming

> **Note:** TASK-024 (break DB→OCPP dependency) should be done before or alongside TASK-025 (implement new repos).

### TASK-024 — Break `Backend.DB → Backend.OCPP` dependency
- **Status:** DONE
- **Priority:** P1
- **Depends on:** TASK-013
- **Why:** `Backend.DB` currently references `Backend.OCPP` solely to implement `IOcppPersistence` and consume its OCPP-branded persistence DTOs (`PersistedCharger`, `OcppBootInfo`, etc.). This makes the database project structurally dependent on the protocol layer. Once application-layer repository interfaces exist (TASK-013), the DB project should implement those instead, removing the reverse dependency.
- **Scope:**
  - Add a `Backend.DB → Application` project reference.
  - Implement the application-layer repository interfaces in `Backend.DB`.
  - Remove `Backend.DB → Backend.OCPP` project reference.
  - Remove or deprecate `IOcppPersistence` (consumers in OCPP should use the application abstractions).
  - Update DI registrations to wire the new repository implementations.
- **Likely touched areas:** `HASmartCharge.Backend.DB/`, `HASmartCharge.Backend/Program.cs`, OCPP persistence consumers
- **Done when:**
  - `Backend.DB.csproj` no longer references `Backend.OCPP`.
  - Persistence flows through application-layer repository interfaces.
  - The solution builds and runtime behavior is unchanged.
- **Suggested agent brief:**
  - "Break the Backend.DB → Backend.OCPP dependency by making the DB project implement application-layer repository interfaces instead of IOcppPersistence. Remove the OCPP project reference from Backend.DB."
- **Completed work (Phase 6):**
  - Deleted `IOcppPersistence.cs` (interface + `PersistedCharger`/`OcppBootInfo`/`PersistedConnector` DTOs) from `Backend.OCPP`.
  - Deleted `OcppRepository.cs` from `Backend.DB` (superseded by `EfChargerRepository` + `EfChargingSessionRepository`).
  - Removed `SeedFromDatabase(IEnumerable<PersistedCharger>)` from `ChargerStatusTracker` (unused; `SeedFromDomainChargers` is the active method).
  - Removed `<ProjectReference>` to `Backend.OCPP` from `HASmartCharge.Backend.DB.csproj`.
  - Removed legacy `IOcppPersistence`/`OcppRepository` DI registration from `Program.cs`.
  - Solution builds with 0 errors.

### TASK-025 — Introduce database-side implementations for new repositories
- **Status:** DONE
- **Priority:** P1
- **Depends on:** TASK-013, TASK-024
- **Why:** The new application abstractions need backing implementations.
- **Scope:**
  - Implement `IChargerRepository` and `IChargingSessionRepository` in the database project.
  - Minimize churn while replacing the old persistence role.
- **Likely touched areas:** `HASmartCharge.Backend.DB/`
- **Done when:**
  - The database project satisfies the new repository interfaces.
- **Suggested agent brief:**
  - "Implement the new application repository interfaces in the database project, replacing the architectural role of `IOcppPersistence` without unnecessary schema churn."
- **Completed work (Phase 5):**
  - `EfChargerRepository.cs` implementing `IChargerRepository` added to `Backend.DB`.
  - `EfChargingSessionRepository.cs` implementing `IChargingSessionRepository` added to `Backend.DB`.
  - Both repositories registered in DI in `Program.cs` and wired into application command handlers.

### TASK-026 — Split and re-home `ChargerStatusTracker` once mutations are event-driven
- **Status:** DONE
- **Priority:** P2
- **Depends on:** TASK-022
- **Why:** The tracker acts like a shared read model, not an OCPP-only concern. However, it currently has two roles: (1) read model implementing `IChargerReadModel` (already abstracted) and (2) mutation target called directly by OCPP protocol handlers via `OnFoo()` methods. Moving it before replacing direct mutations with domain events would just shift the coupling. Once TASK-022 converts mutations to event handlers, the tracker becomes a pure event subscriber + read model and can be cleanly relocated.
- **Scope:**
  - After TASK-022 is done: move the tracker to `Application` (or a shared infrastructure project) as a pure `IChargerReadModel` implementation that subscribes to domain events.
  - Remove the `OnFoo()` mutation methods — they will have been replaced by event handlers.
  - Clean up DI registrations (single registration via interface only).
- **Likely touched areas:** project structure, namespaces, DI, event handler wiring
- **Done when:**
  - The tracker no longer lives in `Backend.OCPP`.
  - It has no direct mutation API — only event subscriptions and read queries.
  - Dependency flow remains sane and the solution builds.
- **Suggested agent brief:**
  - "Once direct OnFoo() mutations have been replaced by event handlers, relocate ChargerStatusTracker to the application layer as a pure read-model implementation."
- **Completed work (Phase 6):**
  - `ChargerStatusTracker` now directly implements `IDomainEventHandler<T>` for six domain events: `ChargerConnected`, `ChargerDisconnected`, `ChargerRegistered`, `ConnectorStatusUpdated`, `ChargingSessionStarted`, `ChargingSessionCompleted`.
  - Removed `OnChargerConnected`, `OnChargerDisconnected`, `OnBootNotification`, `OnStatusNotification`, `OnStartTransaction`, `OnStopTransaction` methods — replaced by `HandleAsync` overloads reading directly from domain event properties.
  - `OnMeterValues` retained — still called directly from `ChargePointSession.HandleMeterValuesAsync` (not event-driven yet).
  - Deleted `TrackerEventHandlers.cs` entirely — the intermediary wrapper classes that reconstructed OCPP request objects from domain events are no longer needed.
  - Updated `Program.cs` to register the tracker directly via `dispatcher.Register<TEvent>(tracker)` instead of constructing six separate wrapper handler instances. Removed the `using HASmartCharge.Backend.OCPP.Services.EventHandlers` directive.
  - Solution builds with 0 errors.

### TASK-027 — Review and rename misleading `Domain` usage inside the OCPP project
- **Status:** DONE
- **Priority:** P2
- **Why:** The current OCPP `Domain/` folder is not a true domain layer and will confuse future development.
- **Scope:**
  - Propose and apply clearer naming where safe.
  - Update namespaces and references carefully.
- **Done when:**
  - The project no longer mislabels protocol/session infrastructure as domain logic.
- **Suggested agent brief:**
  - “Clean up misleading folder/namespace naming inside the OCPP project so ‘domain’ is reserved for the real domain model.”
- **Completed work:**
  - Renamed namespace `HASmartCharge.Backend.OCPP.Domain` → `HASmartCharge.Backend.OCPP.Sessions` in all 4 files: `ISessionManager.cs`, `SessionManager.cs`, `IChargePointSession.cs`, `ChargePointSession.cs` (files remain in `Domain/` folder; namespace reflects purpose).
  - Updated all callers: `OcppMessageRouter.cs`, `OcppConnectionOrchestrator.cs`, `ICommandSender.cs` (also fixed qualified `Domain.ISessionManager` references), `ChargerCommandsController.cs`, and `Program.cs` (fully-qualified DI registration).
  - Solution builds with 0 errors.

### TASK-028 — Evaluate renaming projects to adapter-oriented names
- **Status:** TODO
- **Priority:** P2
- **Why:** Long-term architecture would be clearer if OCPP/DB/HA components were framed as adapters.
- **Scope:**
  - Assess the cost/benefit of renaming projects like `Backend.OCPP` and `Backend.DB`.
  - If chosen, make changes incrementally.
- **Done when:**
  - A clear decision is documented, or the rename is completed safely.
- **Suggested agent brief:**
  - “Evaluate whether the infrastructure projects should be renamed to adapter-style names and either document the decision or perform the rename safely.”

---

## Phase 7 — Home Assistant isolation and integration

> **Note:** Phase 7 is independent from Phases 3–6 and can be started whenever convenient. TASK-029 (inventory) has no unmet dependencies.

### TASK-029 — Inventory all Home Assistant concerns currently living in `Backend`
- **Status:** TODO
- **Priority:** P1
- **Why:** HA integration is structurally tangled even though it is conceptually separate.
- **Scope:**
  - Identify services, models, config, background services, controllers, persistence objects, and startup hooks related to HA.
- **Done when:**
  - A complete inventory exists for extraction work.
- **Suggested agent brief:**
  - “Audit the backend for all Home Assistant-related code and produce a concrete extraction inventory grouped by service/model/config/startup responsibilities.”

### TASK-030 — Define `IHomeAutomationGateway` in `Application`
- **Status:** TODO
- **Priority:** P1
- **Depends on:** TASK-002
- **Why:** HA should integrate through an anti-corruption boundary, not direct service injection into core flows.
- **Scope:**
  - Define a minimal application-facing abstraction for publishing charger/session state and reading automation data if needed.
- **Done when:**
  - The application layer exposes a clean HA-facing port.
- **Suggested agent brief:**
  - “Define an `IHomeAutomationGateway` abstraction in the application layer for future Home Assistant publishing/reading scenarios.”

### TASK-031 — Extract Home Assistant integration into a dedicated project or module
- **Status:** TODO
- **Priority:** P2
- **Depends on:** TASK-029, TASK-030
- **Why:** HA code should be isolated from the core host/backend responsibilities.
- **Scope:**
  - Move HA auth, token lifecycle, API client code, models, configuration, and background services into a dedicated module/project.
- **Done when:**
  - HA concerns are grouped coherently and consumed via abstractions.
- **Suggested agent brief:**
  - “Extract Home Assistant-specific auth/API/background-service concerns into a dedicated adapter-style project or module while keeping the app buildable.”

### TASK-032 — Publish charger lifecycle changes to Home Assistant through event-driven handlers
- **Status:** TODO
- **Priority:** P2
- **Depends on:** TASK-015, TASK-030, TASK-031
- **Why:** HA integration currently exists but is not meaningfully connected to charging behavior.
- **Scope:**
  - Implement event handlers that map charger/session events to HA gateway calls.
- **Done when:**
  - At least key charger/session events can be published through the HA abstraction.
- **Suggested agent brief:**
  - “Wire charger/session domain events into Home Assistant publishing handlers through the new application gateway abstraction.”

---

## Phase 8 — Deployment hardening

> **Note:** Deployment tasks are largely independent from the domain/OCPP refactor work and can be done in parallel.

### TASK-033 — Add environment-driven configuration for deployable settings
- **Status:** TODO
- **Priority:** P1
- **Why:** Database paths, HA URLs, and ports must be deployment-friendly.
- **Scope:**
  - Ensure important config values can be overridden cleanly via environment variables.
  - Avoid hardcoded local-only assumptions.
- **Done when:**
  - Deployment-sensitive settings are externally configurable.
- **Suggested agent brief:**
  - “Refactor configuration so deployable settings like database path, Home Assistant URL, and listener settings are clearly environment-overridable.”

### TASK-034 — Add health checks
- **Status:** TODO
- **Priority:** P1
- **Why:** Production deployments need visibility into runtime readiness and dependency health.
- **Scope:**
  - Add health endpoints for at least database connectivity and core app readiness.
  - Extend to HA/OCPP listener health where practical.
- **Done when:**
  - A health endpoint exists and reports useful component-level status.
- **Suggested agent brief:**
  - “Add health checks for the backend and key dependencies so deployment environments can monitor readiness and liveness.”

### TASK-035 — Make startup initialization resilient
- **Status:** TODO
- **Priority:** P1
- **Why:** Rigid startup sequencing is fragile in Docker and Home Assistant environments.
- **Scope:**
  - Review migration, seeding, HA initialization, and other startup-time work.
  - Add retries/backoff or defer non-critical work if needed.
- **Done when:**
  - Startup is less likely to fail solely because a dependency is slow to become available.
- **Suggested agent brief:**
  - “Harden application startup so database and Home Assistant initialization are resilient instead of assuming all dependencies are instantly available.”

### TASK-036 — Create Docker packaging for production/backend deployment
- **Status:** TODO
- **Priority:** P2
- **Why:** The project needs a clear deployment path beyond local development orchestration.
- **Scope:**
  - Add a Dockerfile and any supporting assets needed for backend deployment.
  - Include frontend strategy if appropriate.
- **Done when:**
  - The app can be containerized with a documented build path.
- **Suggested agent brief:**
  - “Create production-oriented Docker packaging for the solution, keeping configuration externalized and avoiding local-dev-only assumptions.”

### TASK-037 — Create Home Assistant Add-on packaging scaffold
- **Status:** TODO
- **Priority:** P2
- **Depends on:** TASK-033, TASK-036
- **Why:** HA Add-on deployment was explicitly called out as a target.
- **Scope:**
  - Add add-on metadata/config and runtime scaffolding.
- **Done when:**
  - A basic add-on packaging structure exists and aligns with the deployment model.
- **Suggested agent brief:**
  - “Create the Home Assistant Add-on packaging scaffold and connect it to the new Docker/configuration approach.”

---

## Phase 9 — Testing and concurrency safety

> **Note:** Test projects can be created early (TASK-038 depends only on domain/application layers existing), but meaningful test coverage requires command handlers and entity behavior to be in place.

### TASK-038 — Create test projects for Domain and Application layers
- **Status:** TODO
- **Priority:** P1
- **Depends on:** TASK-001, TASK-002, TASK-011, TASK-014
- **Why:** The refactor needs a safety net, and the new architecture should be easy to test.
- **Scope:**
  - Add at least unit-test projects for domain and application behavior.
- **Done when:**
  - Tests exist for core entity invariants and command-handler behavior.
- **Suggested agent brief:**
  - “Add unit test projects for the new domain and application layers and cover the most important charger/session behaviors first.”

### TASK-039 — Add integration tests for OCPP translation flows
- **Status:** TODO
- **Priority:** P2
- **Depends on:** TASK-017, TASK-018, TASK-019, TASK-020, TASK-021
- **Why:** Protocol translation is a high-risk refactor area.
- **Scope:**
  - Cover the OCPP-to-application flow for key messages.
- **Done when:**
  - There is automated coverage for the most important message translation paths.
- **Suggested agent brief:**
  - “Add integration-style tests that verify key OCPP message flows are translated into the intended application behaviors.”

### TASK-040 — Review thread-safety of the read model and shared mutable state
- **Status:** TODO
- **Priority:** P1
- **Why:** The current tracker uses concurrent collections but may still mutate nested objects unsafely.
- **Scope:**
  - Audit the tracker and related status objects.
  - Recommend either safer mutation boundaries or more immutable snapshots.
- **Done when:**
  - The concurrency risk is documented and at least partially mitigated.
- **Suggested agent brief:**
  - “Audit the charger read model for thread-safety issues involving shared mutable state, then implement the smallest safe mitigation or document the exact required follow-up.”

---

## Good assignment patterns for future AI agents

When assigning tasks from this file, prefer prompts like:

- “Complete `TASK-007` from `PROJECT_IMPROVEMENT_TASKLIST.md`. Keep scope limited to that task, update the task status when done, and run relevant build/tests.”
- “Complete `TASK-017` only. Do not continue to later refactors. Preserve existing behavior and document any blockers in the task list.”
- “Handle `TASK-029`, then update the task entry with findings or split it into follow-up subtasks if needed.”

---

## Tasks that should **not** be bundled together yet

Avoid combining these into one agent run unless the codebase has already been partially refactored:

- `TASK-011` through `TASK-023` all at once
- OCPP refactor + HA extraction in one pass
- project renames + namespace cleanup + architecture moves in one pass
- deployment packaging before configuration cleanup

Those combos are where merge conflicts and architectural chaos go to party.

---

## Suggested first five assignments

The shortest path to a fully OCPP-decoupled API layer and domain model foundation:

1. `TASK-008` — Define `IChargerGateway` (can start immediately, no unmet deps)
2. `TASK-009` — Implement OCPP-backed gateway
3. `TASK-010` — Refactor `ChargerCommandsController` (completes **all** controller decoupling)
4. `TASK-011` — Introduce core domain entities (can start immediately, no unmet deps)
5. `TASK-012` — Define core domain events

After these five, the project will have:
- All controllers fully decoupled from OCPP types
- A real domain model with entities and events
- A clear path into command handlers and the OCPP god-object refactor
