import { apiFetch } from '@/api/client'
import type { ChargerStatus, CommandResult } from '@/types/charger'

export function getChargerStatus(): Promise<ChargerStatus> {
  return apiFetch<ChargerStatus>('/api/charger/status')
}

export function unlockCharger(): Promise<CommandResult> {
  return apiFetch<CommandResult>('/api/charger/unlock', { method: 'POST' })
}

export function setChargerAvailability(available: boolean): Promise<CommandResult> {
  return apiFetch<CommandResult>('/api/charger/availability', {
    method: 'POST',
    body: JSON.stringify({ available }),
  })
}

export function reconfigureCharger(): Promise<CommandResult> {
  return apiFetch<CommandResult>('/api/charger/reconfigure', { method: 'POST' })
}

export interface SetPowerResult {
  chargePointId: string
  setpointKw: number
  status: string | null
}

export function setChargerPower(kw: number): Promise<SetPowerResult> {
  return apiFetch<SetPowerResult>('/api/charger/power', {
    method: 'POST',
    body: JSON.stringify({ kw }),
  })
}
