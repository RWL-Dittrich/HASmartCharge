import { apiFetch } from '@/api/client'
import type { ZaptecCharger, ZaptecStatus } from '@/types/zaptec'

export function getZaptecChargers(): Promise<ZaptecCharger[]> {
  return apiFetch<ZaptecCharger[]>('/api/zaptec/chargers')
}

export function getZaptecStatus(): Promise<ZaptecStatus> {
  return apiFetch<ZaptecStatus>('/api/zaptec/status')
}
