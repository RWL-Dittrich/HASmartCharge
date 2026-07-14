export interface MqttStatus {
  enabled: boolean
  connected: boolean
  host: string
  port: number
  lastConnectedAt: string | null
  lastPublishAt: string | null
  lastError: string | null
  lastErrorAt: string | null
}

export interface MqttTestResult {
  success: boolean
  error: string | null
}
