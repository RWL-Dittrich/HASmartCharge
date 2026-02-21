# OCPP 1.6J WebSocket Server

## Overview

The HASmartCharge backend includes a complete custom implementation of an OCPP 1.6J (Open Charge Point Protocol JSON over WebSocket) server built from scratch following the official OCPP specification. This allows electric vehicle charge points to connect and communicate with the system using the industry-standard OCPP protocol.

## Implementation

This is a **ground-up implementation** of the OCPP 1.6J protocol, built specifically for HASmartCharge without relying on third-party OCPP libraries. The implementation follows the official OCPP 1.6J specification and reference guides.

## Features

- **Full OCPP 1.6J protocol implementation** built from scratch
- **Complete schema support** for all OCPP 1.6J message types
- WebSocket-based communication with `ocpp1.6` sub-protocol
- JSON message format with proper request/response handling
- Support for all standard OCPP 1.6J messages:
  - **BootNotification**: Charge point registration and configuration
  - **Authorize**: RFID tag authorization
  - **StartTransaction**: Begin a charging session
  - **StopTransaction**: End a charging session
  - **Heartbeat**: Keep-alive messages
  - **MeterValues**: Energy consumption reporting
  - **StatusNotification**: Charge point status updates
  - **DataTransfer**: Vendor-specific data exchange
  - **DiagnosticsStatusNotification**: Diagnostics status reporting
  - **FirmwareStatusNotification**: Firmware update status

## Connection Details

### WebSocket Endpoint

Charge points connect to the OCPP server using the following WebSocket URL format:

```
ws://<hostname>:<port>/ocpp/1.6/{chargePointId}
```

Or with TLS:

```
wss://<hostname>:<port>/ocpp/1.6/{chargePointId}
```

Where:
- `{chargePointId}` is the unique identifier for the charge point

### Example

For a charge point with ID `CP001` connecting to a server at `example.com:5000`:

```
ws://example.com:5000/ocpp/1.6/CP001
```

### Sub-protocol

The WebSocket handshake must include the OCPP 1.6 sub-protocol:

```
Sec-WebSocket-Protocol: ocpp1.6
```

## Message Format

OCPP 1.6J uses JSON-RPC style message frames:

### CALL (Request from charge point)
```json
[2, "unique-id", "ActionName", { "payload": "data" }]
```

### CALLRESULT (Response to charge point)
```json
[3, "unique-id", { "payload": "data" }]
```

### CALLERROR (Error response)
```json
[4, "unique-id", "ErrorCode", "Error description", { "details": "data" }]
```

## Supported Operations

### Core Profile

| Operation | Direction | Description |
|-----------|-----------|-------------|
| Authorize | CP → CS | Authorize an RFID tag |
| BootNotification | CP → CS | Register charge point with central system |
| DataTransfer | CP ↔ CS | Exchange vendor-specific data |
| Heartbeat | CP → CS | Send keep-alive message |
| MeterValues | CP → CS | Report energy meter readings |
| StartTransaction | CP → CS | Start a charging transaction |
| StatusNotification | CP → CS | Report charge point status |
| StopTransaction | CP → CS | Stop a charging transaction |

### Firmware Management Profile

| Operation | Direction | Description |
|-----------|-----------|-------------|
| DiagnosticsStatusNotification | CP → CS | Report diagnostics upload status |
| FirmwareStatusNotification | CP → CS | Report firmware update status |

*CP = Charge Point, CS = Central System*

## Configuration

The OCPP server is automatically configured when the application starts. Key settings:

- **Heartbeat Interval**: 60 seconds (returned in BootNotification response)
- **Authorization**: All RFID tags are accepted by default with 24-hour expiry
- **Transaction IDs**: Randomly generated unique identifiers

## Testing

### Using a WebSocket Client

You can test the OCPP server using a WebSocket client tool (like `wscat`):

1. Install wscat:
   ```bash
   npm install -g wscat
   ```

2. Connect to the server:
   ```bash
   wscat -c "ws://localhost:5000/ocpp/1.6/TEST001" -s ocpp1.6
   ```

3. Send a BootNotification message:
   ```json
   [2, "1", "BootNotification", {"chargePointVendor": "VendorName", "chargePointModel": "ModelName"}]
   ```

4. Expected response:
   ```json
   [3, "1", {"status": "Accepted", "currentTime": "2024-01-01T12:00:00Z", "interval": 60}]
   ```

### Using OCPP Test Tools

You can also use dedicated OCPP testing tools like:
- **OCPP-J Test Tool** - Official test suite from Open Charge Alliance
- **EV Charge Sim** - Charge point simulator
- **SteVe** - OCPP server with built-in testing capabilities

## Implementation Details

### Architecture

The OCPP implementation follows a clean layered architecture with clear separation of concerns:

#### Layer Overview

```
┌─────────────────────────────────────────────────────────┐
│                     OcppController                      │
│              (ASP.NET WebSocket Endpoint)               │
└─────────────────────┬───────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────┐
│             OcppConnectionOrchestrator                  │
│              (Infrastructure Layer)                      │
│   • Creates sessions and connections                    │
│   • Manages connection lifecycle                        │
│   • Delegates to Router and SessionManager              │
└─────────────────────┬───────────────────────────────────┘
                      │
        ┌─────────────┼─────────────┐
        │             │             │
        ▼             ▼             ▼
┌──────────────┐ ┌────────────┐ ┌─────────────────┐
│  Transport   │ │ Application│ │     Domain      │
│    Layer     │ │   Layer    │ │     Layer       │
└──────────────┘ └────────────┘ └─────────────────┘
```

#### 1. Transport Layer
**Responsibility**: Manage physical connection, no business logic

**Key Components:**
- `IConnection` - Abstraction for any transport (WebSocket, TCP, etc.)
- `WebSocketConnection` - WebSocket implementation of IConnection
- Handles send/receive at the byte level
- Completely isolated from OCPP protocol details

**Files:**
- `Transport/IConnection.cs`
- `Transport/WebSocketConnection.cs`
- `Services/WebSocketMessageService.cs` (low-level WebSocket buffering)

#### 2. Application Layer
**Responsibility**: Route messages, decode/encode OCPP protocol

**Key Components:**
- `IOcppMessageRouter` / `OcppMessageRouter` - Routes OCPP messages to sessions
- Parses OCPP message types (CALL, CALLRESULT, CALLERROR)
- Handles message correlation and response generation
- No domain state or business logic

**Message Flow:**
1. Receive raw JSON string from transport
2. Parse into `OcppMessage` (MessageType, MessageId, Action, Payload)
3. Route to appropriate `ChargePointSession` based on connection
4. Handle response/error formatting

**Files:**
- `Application/IOcppMessageRouter.cs`
- `Application/OcppMessageRouter.cs`
- `Models/OcppMessage.cs` (message parsing/serialization)

#### 3. Domain Layer
**Responsibility**: Charge point business logic and state

**Key Components:**
- `IChargePointSession` / `ChargePointSession` - Represents a connected charge point
  - Owns per-charge-point state (transactions, status, configuration)
  - Handles all inbound OCPP actions (BootNotification, Heartbeat, StartTransaction, etc.)
  - Provides outbound command methods (SetAvailability, RemoteStart/Stop, ChangeConfiguration)
  - No WebSocket dependencies - uses IConnection abstraction

- `ISessionManager` / `SessionManager` - Tracks all active sessions
  - Maps charge point IDs to sessions
  - Maps connection IDs to sessions
  - Provides session lookup and lifecycle management

**State Management:**
- Session state lives in `ChargePointSession`
- Aggregated state tracking via `ChargerStatusTracker` (shared service)
- Configuration application via `ChargerConfigurationService`

**Files:**
- `Domain/IChargePointSession.cs`
- `Domain/ChargePointSession.cs`
- `Domain/ISessionManager.cs`
- `Domain/SessionManager.cs`
- `Services/ChargerStatusTracker.cs`
- `Services/ChargerConfigurationService.cs`

#### 4. Infrastructure Layer
**Responsibility**: Orchestration and cross-cutting concerns

**Key Components:**
- `OcppConnectionOrchestrator` - Coordinates connection handling
  - Creates WebSocketConnection from raw WebSocket
  - Creates ChargePointSession with proper dependencies
  - Registers session with SessionManager
  - Manages message processing loop
  - Handles cleanup on disconnect

**Files:**
- `Infrastructure/OcppConnectionOrchestrator.cs`

### Message Flow (Detailed)

**Inbound Message (CP → CS):**
```
1. WebSocket.ReceiveAsync (raw bytes)
   ↓
2. WebSocketMessageService.ReceiveMessageAsync (string)
   ↓
3. OcppConnectionOrchestrator.ProcessMessagesAsync
   ↓
4. OcppMessageRouter.RouteAsync
   ↓ (parse OcppMessage)
5. SessionManager.GetByConnectionId
   ↓
6. ChargePointSession.HandleCallAsync
   ↓ (switch on action)
7. ChargePointSession.HandleBootNotificationAsync (or other handler)
   ↓
8. ChargerStatusTracker.OnBootNotification (update state)
   ↓
9. Return response object
   ↓
10. OcppMessageRouter wraps in CALLRESULT
    ↓
11. WebSocketConnection.SendAsync
    ↓
12. WebSocketMessageService.SendMessageAsync
```

**Outbound Command (CS → CP):**
```
1. External caller → SessionManager.GetByChargePointId
   ↓
2. ChargePointSession.ChangeConfigurationAsync (or other command)
   ↓
3. ChargePointSession.SendCommandAsync<T>
   ↓ (create CALL message)
4. IConnection.SendAsync
   ↓
5. WebSocketMessageService.SendMessageAsync
   ↓
6. WebSocket.SendAsync (raw bytes)
```

### Dependency Rules

These are **hard constraints** enforced by the architecture:

1. **Domain must NOT depend on Transport**
   - ChargePointSession uses `IConnection`, never `WebSocket`
   - No ASP.NET types in domain code

2. **Transport must NOT contain OCPP logic**
   - WebSocketConnection only knows about sending/receiving strings
   - No knowledge of CALL/CALLRESULT/actions

3. **Application routes, does NOT own state**
   - Router delegates to sessions for all business logic
   - No transaction counters or status tracking in router

4. **No circular dependencies**
   - Clean one-way dependency graph:
     `Infrastructure → Application → Domain → Transport (abstraction only)`

### Configuration Application Flow

Configuration is applied **after BootNotification** is received:

1. Connection established
2. Session created and registered
3. `Session.InitializeAsync()` called
4. After 2s delay → TriggerMessage(BootNotification) sent to CP
5. CP responds with BootNotification → session handles it
6. After additional 2s delay → `ChargerConfigurationService.ConfigureChargerAsync()` called
7. Configuration commands (ChangeConfiguration) sent to CP

This ensures the charge point is fully initialized before we attempt configuration.

### Implementation Components

**Project Structure:**
- **HASmartCharge.Backend.OCPP** - Separate class library project containing all OCPP logic
  - **Transport/**: Connection abstractions (IConnection, WebSocketConnection)
  - **Application/**: Message routing and protocol handling (OcppMessageRouter)
  - **Domain/**: Charge point sessions and business logic (ChargePointSession, SessionManager)
  - **Infrastructure/**: Orchestration and cross-cutting concerns (OcppConnectionOrchestrator)
  - **Models/**: Individual files for each OCPP message type
  - **Services/**: Shared services (ChargerStatusTracker, ChargerConfigurationService, WebSocketMessageService)

**Key Files:**

1. **Transport Layer:**
   - `Transport/IConnection.cs` - Connection abstraction
   - `Transport/WebSocketConnection.cs` - WebSocket implementation
   - `Services/WebSocketMessageService.cs` - Low-level WebSocket buffering

2. **Application Layer:**
   - `Application/OcppMessageRouter.cs` - Routes OCPP messages to sessions
   - `Models/OcppMessage.cs` - Core OCPP message parsing and serialization

3. **Domain Layer:**
   - `Domain/ChargePointSession.cs` - Charge point session with all OCPP handlers
   - `Domain/SessionManager.cs` - Tracks and manages all active sessions

4. **Infrastructure Layer:**
   - `Infrastructure/OcppConnectionOrchestrator.cs` - Connection lifecycle orchestration
   - `Services/ChargerStatusTracker.cs` - Aggregated state tracking
   - `Services/ChargerConfigurationService.cs` - Automatic charger configuration

5. **Controller (Entry Point):**
   - `HASmartCharge.Backend/Controllers/OcppController.cs` - ASP.NET Core WebSocket endpoint

### Dependencies

**No external OCPP libraries required!** This is a pure C# implementation using only:
- ASP.NET Core WebSocket support
- System.Text.Json for JSON serialization
- Microsoft.Extensions.Logging for logging
- Standard .NET libraries

### Logging

The OCPP server logs all important events:
- Connection/disconnection events (Information level)
- OCPP messages received and sent (Debug level)
- Transaction events (Information level)
- Errors and warnings (Warning/Error level)

## Future Enhancements

Planned features for future versions:
- Persistent storage of charge point connections
- Advanced authorization with database lookup
- Smart charging profile management
- Remote commands (RemoteStartTransaction, RemoteStopTransaction, etc.)
- Integration with Home Assistant entities
- Charge scheduling based on electricity prices

## References

- [OCPP 1.6J Specification](https://www.openchargealliance.org/protocols/ocpp-16/)
- [OCPP 1.6 JSON Schemas](https://ocpp-spec.org/schemas/v1.6/)
- [Complete OCPP 1.6 WebSocket Guide](https://gist.github.com/rohittiwari-dev/1bed980b1ca21e5a0a09c20bdfd7f9fa)
- [OCPP.md Documentation](https://ocpp.md/ocpp-1.6j/)
