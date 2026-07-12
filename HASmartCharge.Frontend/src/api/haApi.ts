import { apiFetch } from '@/api/client'
import type { HaEntity, HaServiceDomain, HaStatus } from '@/types/ha'

export function getHaStatus(): Promise<HaStatus> {
  return apiFetch<HaStatus>('/api/ha/status')
}

export function getHaEntities(domain?: string): Promise<HaEntity[]> {
  const params = domain ? `?domain=${encodeURIComponent(domain)}` : ''
  return apiFetch<HaEntity[]>(`/api/ha/entities${params}`)
}

export function getHaServices(): Promise<HaServiceDomain[]> {
  return apiFetch<HaServiceDomain[]>('/api/ha/services')
}

/** Redirects the browser to the HA OAuth authorization flow. */
export function startHaConnect(baseUrl: string): void {
  window.location.href = `/api/homeassistant/auth/start?baseUrl=${encodeURIComponent(baseUrl)}`
}

export function disconnectHa(): Promise<{ success: boolean; message: string }> {
  return apiFetch('/api/homeassistant/auth/disconnect', { method: 'POST' })
}
