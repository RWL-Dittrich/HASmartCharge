import { apiFetch } from '@/api/client'
import type { ZaptecApiCallRequest, ZaptecApiCallResult, ZaptecCharger, ZaptecStatus } from '@/types/zaptec'

export function getZaptecChargers(): Promise<ZaptecCharger[]> {
  return apiFetch<ZaptecCharger[]>('/api/zaptec/chargers')
}

export function getZaptecStatus(): Promise<ZaptecStatus> {
  return apiFetch<ZaptecStatus>('/api/zaptec/status')
}

export function sendZaptecApiCall(request: ZaptecApiCallRequest): Promise<ZaptecApiCallResult> {
  return apiFetch<ZaptecApiCallResult>('/api/zaptec/api-call', {
    method: 'POST',
    body: JSON.stringify(request),
  })
}
