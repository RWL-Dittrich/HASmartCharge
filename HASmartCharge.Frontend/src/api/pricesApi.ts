import { apiFetch } from '@/api/client'
import type { HourlyPrice, PriceFetchResult } from '@/types/prices'

export function getPrices(from?: string, to?: string): Promise<HourlyPrice[]> {
  const params = new URLSearchParams()
  if (from) params.set('from', from)
  if (to) params.set('to', to)
  const query = params.toString()
  return apiFetch<HourlyPrice[]>(`/api/prices${query ? `?${query}` : ''}`)
}

export function refreshPrices(): Promise<PriceFetchResult> {
  return apiFetch<PriceFetchResult>('/api/prices/refresh', { method: 'POST' })
}
