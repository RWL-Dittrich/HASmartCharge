import { apiFetch } from '@/api/client'

export interface ChargeOverrideResult {
  overrideUntilUtc: string
}

export function startCharge(overrideMinutes = 60): Promise<ChargeOverrideResult> {
  return apiFetch<ChargeOverrideResult>(`/api/charge/start?overrideMinutes=${overrideMinutes}`, {
    method: 'POST',
  })
}

export function stopCharge(overrideMinutes = 60): Promise<ChargeOverrideResult> {
  return apiFetch<ChargeOverrideResult>(`/api/charge/stop?overrideMinutes=${overrideMinutes}`, {
    method: 'POST',
  })
}
