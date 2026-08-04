export interface ChargerStatus {
  chargePointId: string
  connected: boolean
  connectorId: number
  connectorStatus: string | null
  currentPowerKw: number | null
  sessionEnergyKwh: number | null
  sessionCost: number | null
  lastHeartbeatAt: string | null
}

export interface CommandResult {
  [key: string]: unknown
}

/** Result of an arbitrary OCPP call sent via the developer panel. */
export interface OcppCallResult {
  success: boolean
  rawPayload?: unknown
  errorCode?: string | null
  errorDescription?: string | null
}

/** A single OCPP frame (request or response) as broadcast over the ocpp-log SignalR hub. */
export interface OcppFrame {
  timestampUtc: string
  chargePointId: string
  direction: 'in' | 'out'
  frame: string
}
