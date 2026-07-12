import { apiFetch } from '@/api/client'
import type { ChargeSessionDetail, ChargeSessionSummary } from '@/types/sessions'

export function getSessions(): Promise<ChargeSessionSummary[]> {
  return apiFetch<ChargeSessionSummary[]>('/api/sessions')
}

export function getSession(transactionId: number): Promise<ChargeSessionDetail> {
  return apiFetch<ChargeSessionDetail>(`/api/sessions/${transactionId}`)
}

export function deleteSession(transactionId: number): Promise<void> {
  return apiFetch<void>(`/api/sessions/${transactionId}`, { method: 'DELETE' })
}
