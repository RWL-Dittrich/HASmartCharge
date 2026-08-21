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

export interface ZaptecApiCallRequest {
  method: 'GET' | 'POST' | 'PUT' | 'DELETE'
  path: string
  body?: string | null
}

export interface ZaptecApiCallResult {
  statusCode: number
  success: boolean
  body: string | null
}
