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
ws://<hostname>:<port>/ocpp16/{chargePointId}
```

Or with TLS:

```
wss://<hostname>:<port>/ocpp16/{chargePointId}
```

Where:
- `{chargePointId}` is the unique identifier for the charge point

### Example

For a charge point with ID `CP001` connecting to a server at `example.com:5000`:

```
ws://example.com:5000/ocpp16/CP001
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
   wscat -c "ws://localhost:5000/ocpp16/TEST001" -s ocpp1.6
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

```
OcppController (WebSocket endpoint)
    ↓
OcppServerService (manages connections and message handling)
    ↓
OcppMessageHandler (routes messages to handlers)
    ├── BootNotification handler
    ├── Authorize handler
    ├── StartTransaction handler
    ├── StopTransaction handler
    ├── Heartbeat handler
    ├── MeterValues handler
    ├── StatusNotification handler
    ├── DataTransfer handler
    ├── DiagnosticsStatusNotification handler
    └── FirmwareStatusNotification handler
```

### Implementation Components

1. **OcppMessage.cs** - Core OCPP message parsing and serialization
   - Handles CALL (2), CALLRESULT (3), and CALLERROR (4) message types
   - JSON array format: `[MessageType, MessageId, Action, Payload]`

2. **OcppModels.cs** - Request/Response models for all OCPP 1.6J messages
   - Strongly-typed C# classes for all message payloads
   - JSON serialization attributes for proper formatting

3. **OcppMessageHandler.cs** - Message routing and handler registration
   - Routes incoming messages to appropriate handlers
   - Generates properly formatted responses
   - Error handling with OCPP error codes

4. **OcppServerService.cs** - WebSocket connection management
   - Manages WebSocket lifecycle
   - Processes incoming/outgoing messages
   - Implements all OCPP 1.6J message handlers

5. **OcppController.cs** - ASP.NET Core WebSocket endpoint
   - Accepts WebSocket connections
   - Validates sub-protocol
   - Delegates to OcppServerService

### Dependencies

**No external OCPP libraries required!** This is a pure C# implementation using only:
- ASP.NET Core WebSocket support
- System.Text.Json for JSON serialization
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
