import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { cancelPlan, createPlan, getCurrentPlan, getPlanPreview } from '@/api/planApi'
import type { CreatePlanRequest } from '@/types/plan'

export const planKeys = {
  current: ['plan', 'current'] as const,
  preview: (deadlineUtc: string, targetSocPercent?: number) =>
    ['plan', 'preview', { deadlineUtc, targetSocPercent }] as const,
}

export function useCurrentPlan() {
  return useQuery({ queryKey: planKeys.current, queryFn: getCurrentPlan, refetchInterval: 10_000 })
}

export function usePlanPreview(deadlineUtc: string | null, targetSocPercent?: number) {
  return useQuery({
    queryKey: planKeys.preview(deadlineUtc ?? '', targetSocPercent),
    queryFn: () => getPlanPreview(deadlineUtc as string, targetSocPercent),
    enabled: Boolean(deadlineUtc),
  })
}

export function useCreatePlan() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: CreatePlanRequest) => createPlan(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: planKeys.current }),
  })
}

export function useCancelPlan() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: cancelPlan,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: planKeys.current }),
  })
}
