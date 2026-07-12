import { useMutation, useQueryClient } from '@tanstack/react-query'
import { startCharge, stopCharge } from '@/api/chargeApi'
import { chargerKeys } from '@/hooks/useCharger'

export function useStartCharge() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (overrideMinutes?: number) => startCharge(overrideMinutes),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: chargerKeys.status }),
  })
}

export function useStopCharge() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (overrideMinutes?: number) => stopCharge(overrideMinutes),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: chargerKeys.status }),
  })
}
