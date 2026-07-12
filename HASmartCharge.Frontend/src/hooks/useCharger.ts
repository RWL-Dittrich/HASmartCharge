import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  getChargerStatus,
  reconfigureCharger,
  setChargerAvailability,
  setChargerPower,
  unlockCharger,
} from '@/api/chargerApi'
import { settingsKeys } from '@/hooks/useSettings'

export const chargerKeys = {
  status: ['charger', 'status'] as const,
}

export function useChargerStatus() {
  return useQuery({ queryKey: chargerKeys.status, queryFn: getChargerStatus, refetchInterval: 10_000 })
}

export function useUnlockCharger() {
  return useMutation({ mutationFn: unlockCharger })
}

export function useSetChargerAvailability() {
  return useMutation({ mutationFn: (available: boolean) => setChargerAvailability(available) })
}

export function useReconfigureCharger() {
  return useMutation({ mutationFn: reconfigureCharger })
}

export function useSetChargerPower() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (kw: number) => setChargerPower(kw),
    // Refetch charger settings so the persisted setpoint (slider seed) stays in sync.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: settingsKeys.charger }),
  })
}
