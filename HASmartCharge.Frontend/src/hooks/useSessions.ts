import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { deleteSession, getSession, getSessions } from '@/api/sessionsApi'

export const sessionKeys = {
  list: ['sessions', 'list'] as const,
  detail: (transactionId: number) => ['sessions', 'detail', transactionId] as const,
}

export function useSessions() {
  return useQuery({ queryKey: sessionKeys.list, queryFn: getSessions })
}

export function useDeleteSession() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (transactionId: number) => deleteSession(transactionId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: sessionKeys.list })
    },
  })
}

export function useSessionDetail(transactionId: number | null) {
  return useQuery({
    queryKey: sessionKeys.detail(transactionId ?? -1),
    queryFn: () => getSession(transactionId as number),
    enabled: transactionId !== null,
  })
}
