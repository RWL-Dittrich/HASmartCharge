import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { getPrices, refreshPrices } from '@/api/pricesApi'

export const priceKeys = {
  list: (from?: string, to?: string) => ['prices', { from, to }] as const,
}

export function usePrices(from?: string, to?: string) {
  return useQuery({
    queryKey: priceKeys.list(from, to),
    queryFn: () => getPrices(from, to),
    refetchInterval: 5 * 60_000,
  })
}

export function useRefreshPrices() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: refreshPrices,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['prices'] }),
  })
}
