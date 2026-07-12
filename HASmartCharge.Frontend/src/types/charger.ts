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
