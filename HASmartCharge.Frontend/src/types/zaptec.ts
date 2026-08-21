export interface ZaptecCharger {
  id: string
  name: string
  deviceId: string | null
  isOnline: boolean
  operatingMode: number
}

export interface ZaptecStatus {
  connected: boolean
  lastPollAt: string | null
  lastError: string | null
  isOnline: boolean | null
  operationMode: number | null
}
