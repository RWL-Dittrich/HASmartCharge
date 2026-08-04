import { apiFetch } from '@/api/client'
import type { ChargerStatus, CommandResult, OcppCallResult } from '@/types/charger'

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

/** Sends an arbitrary OCPP 1.6 call straight to the charger (developer tooling; bypasses IChargerControl). */
export function sendOcppCall(action: string, payload: unknown): Promise<OcppCallResult> {
  return apiFetch<OcppCallResult>('/api/charger/ocpp/call', {
    method: 'POST',
    body: JSON.stringify({ action, payload }),
  })
}
