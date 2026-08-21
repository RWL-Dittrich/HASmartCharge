import { useMutation, useQuery } from '@tanstack/react-query'
import { getZaptecChargers, getZaptecStatus, sendZaptecApiCall } from '@/api/zaptecApi'

export const zaptecKeys = {
  status: ['zaptec', 'status'] as const,
  chargers: ['zaptec', 'chargers'] as const,
}

export function useZaptecStatus(enabled: boolean) {
  return useQuery({ queryKey: zaptecKeys.status, queryFn: getZaptecStatus, enabled, refetchInterval: 10_000 })
}

/** Loaded on demand via refetch() (a "Load chargers" button), not on mount. */
export function useZaptecChargers() {
  return useQuery({ queryKey: zaptecKeys.chargers, queryFn: getZaptecChargers, enabled: false })
}

export function useSendZaptecApiCall() {
  return useMutation({ mutationFn: sendZaptecApiCall })
}
