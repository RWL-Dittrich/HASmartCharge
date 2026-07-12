import { apiFetch } from '@/api/client'
import type {
  AutoScheduleSettings,
  OverrideRequest,
  ScheduleOverride,
  UpdateAutoScheduleRequest,
} from '@/types/autoSchedule'

export function getAutoSchedule(): Promise<AutoScheduleSettings> {
  return apiFetch<AutoScheduleSettings>('/api/auto-schedule')
}

export function updateAutoSchedule(request: UpdateAutoScheduleRequest): Promise<AutoScheduleSettings> {
  return apiFetch<AutoScheduleSettings>('/api/auto-schedule', {
    method: 'PUT',
    body: JSON.stringify(request),
  })
}

export function upsertOverride(request: OverrideRequest): Promise<ScheduleOverride> {
  return apiFetch<ScheduleOverride>('/api/auto-schedule/overrides', {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

export function deleteOverride(id: number): Promise<void> {
  return apiFetch<void>(`/api/auto-schedule/overrides/${id}`, { method: 'DELETE' })
}
