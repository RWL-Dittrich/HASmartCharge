import { useQuery } from '@tanstack/react-query'
import { getDashboardSummary } from '@/api/chargersApi'

export const dashboardKeys = {
  summary: ['dashboard', 'summary'] as const,
}

/**
 * Fetches the aggregated dashboard summary (charger counts,
 * connector status breakdown, active transactions, power/energy totals).
 */
export function useDashboardSummary() {
  return useQuery({
    queryKey: dashboardKeys.summary,
    queryFn: getDashboardSummary,
    refetchInterval: 5_000,
  })
}
