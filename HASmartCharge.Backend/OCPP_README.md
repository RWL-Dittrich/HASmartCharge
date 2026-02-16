# OCPP 1.6J WebSocket Server

## Overview

The HASmartCharge backend includes a full implementation of an OCPP 1.6J (Open Charge Point Protocol JSON over WebSocket) server. This allows electric vehicle charge points to connect and communicate with the system using the industry-standard OCPP protocol.

## Features

- Full OCPP 1.6J schema support using the GoldsparkIT.OCPP library
- WebSocket-based communication with `ocpp1.6` sub-protocol
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
OcppServerFactory (creates server instances per connection)
    ↓
OcppServer (handles OCPP messages)
    ├── OcppWebSocketHandler (manages WebSocket communication)
    └── JsonServerHandler (OCPP protocol handling from GoldsparkIT.OCPP)
```

### Dependencies

- **GoldsparkIT.OCPP** (v1.0.4): OCPP 1.6J protocol implementation
- **GoldsparkIT.OCPP.Models** (v1.0.4): OCPP message models and schemas

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
- [GoldsparkIT.OCPP on NuGet](https://www.nuget.org/packages/GoldsparkIT.OCPP)
