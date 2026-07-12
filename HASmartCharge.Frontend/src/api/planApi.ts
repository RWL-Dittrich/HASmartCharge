import { apiFetch, ApiError } from '@/api/client'
import type { ChargePlan, CreatePlanRequest, PlanPreview } from '@/types/plan'

export async function getCurrentPlan(): Promise<ChargePlan | null> {
  try {
    return await apiFetch<ChargePlan>('/api/plan')
  } catch (err) {
    if (err instanceof ApiError && err.status === 404) return null
    throw err
  }
}

export function getPlanPreview(deadlineUtc: string, targetSocPercent?: number): Promise<PlanPreview> {
  const params = new URLSearchParams({ deadline: deadlineUtc })
  if (targetSocPercent !== undefined) params.set('targetSoc', String(targetSocPercent))
  return apiFetch<PlanPreview>(`/api/plan/preview?${params.toString()}`)
}

export function createPlan(request: CreatePlanRequest): Promise<ChargePlan> {
  return apiFetch<ChargePlan>('/api/plan', {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

export function cancelPlan(): Promise<void> {
  return apiFetch<void>('/api/plan', { method: 'DELETE' })
}
