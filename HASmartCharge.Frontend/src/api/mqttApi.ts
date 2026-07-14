import { apiFetch } from '@/api/client'
import type { MqttStatus, MqttTestResult } from '@/types/mqtt'

export function getMqttStatus(): Promise<MqttStatus> {
  return apiFetch<MqttStatus>('/api/mqtt/status')
}

/** One-shot connectivity check against the last saved settings. */
export function testMqtt(): Promise<MqttTestResult> {
  return apiFetch<MqttTestResult>('/api/mqtt/test', { method: 'POST' })
}
