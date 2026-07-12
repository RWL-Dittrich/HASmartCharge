import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { disconnectHa, getHaEntities, getHaServices, getHaStatus } from '@/api/haApi'

export const haKeys = {
  status: ['ha', 'status'] as const,
  entities: (domain?: string) => ['ha', 'entities', { domain }] as const,
  services: ['ha', 'services'] as const,
}

export function useHaStatus() {
  return useQuery({ queryKey: haKeys.status, queryFn: getHaStatus, refetchInterval: 30_000 })
}

export function useHaEntities(domain?: string) {
  return useQuery({ queryKey: haKeys.entities(domain), queryFn: () => getHaEntities(domain) })
}

export function useHaServices() {
  return useQuery({ queryKey: haKeys.services, queryFn: getHaServices, staleTime: 5 * 60_000 })
}

export function useDisconnectHa() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: disconnectHa,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: haKeys.status }),
  })
}
