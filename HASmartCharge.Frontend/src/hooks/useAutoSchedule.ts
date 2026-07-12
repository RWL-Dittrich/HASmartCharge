import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  deleteOverride,
  getAutoSchedule,
  updateAutoSchedule,
  upsertOverride,
} from '@/api/autoScheduleApi'
import type { OverrideRequest, UpdateAutoScheduleRequest } from '@/types/autoSchedule'

export const autoScheduleKeys = {
  root: ['autoSchedule'] as const,
}

export function useAutoSchedule() {
  return useQuery({ queryKey: autoScheduleKeys.root, queryFn: getAutoSchedule })
}

export function useUpdateAutoSchedule() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: UpdateAutoScheduleRequest) => updateAutoSchedule(request),
    onSuccess: (data) => queryClient.setQueryData(autoScheduleKeys.root, data),
  })
}

export function useUpsertOverride() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: OverrideRequest) => upsertOverride(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: autoScheduleKeys.root }),
  })
}

export function useDeleteOverride() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => deleteOverride(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: autoScheduleKeys.root }),
  })
}
