import { useQuery } from '@tanstack/react-query'
import {
  getCharger,
  getChargers,
  getConnector,
  getConnectors,
  getTransactions,
} from '@/api/chargersApi'

// ---------------------------------------------------------------------------
// Query key factory — keeps all cache keys in one place
// ---------------------------------------------------------------------------

export const chargerKeys = {
  all: ['chargers'] as const,
  list: (connected?: boolean) => ['chargers', 'list', { connected }] as const,
  detail: (chargerId: string) => ['chargers', 'detail', chargerId] as const,
  connectors: (chargerId: string) => ['chargers', 'connectors', chargerId] as const,
  connector: (chargerId: string, connectorId: number) =>
    ['chargers', 'connector', chargerId, connectorId] as const,
  transactions: (chargerId: string) => ['chargers', 'transactions', chargerId] as const,
}

// ---------------------------------------------------------------------------
// Hooks
// ---------------------------------------------------------------------------

/**
 * Fetches the full list of chargers.
 * Pass `connected` to filter to only online or offline chargers.
 */
export function useChargers(connected?: boolean) {
  return useQuery({
    queryKey: chargerKeys.list(connected),
    queryFn: () => getChargers(connected),
    refetchInterval: 10_000, // poll every 10 s to reflect connection changes
  })
}

/**
 * Fetches detailed status for a single charger including connectors and measurands.
 * The query is disabled when `chargerId` is empty.
 */
export function useCharger(chargerId: string) {
  return useQuery({
    queryKey: chargerKeys.detail(chargerId),
    queryFn: () => getCharger(chargerId),
    enabled: Boolean(chargerId),
    refetchInterval: 5_000,
  })
}

/**
 * Fetches all connectors for a charger.
 */
export function useConnectors(chargerId: string) {
  return useQuery({
    queryKey: chargerKeys.connectors(chargerId),
    queryFn: () => getConnectors(chargerId),
    enabled: Boolean(chargerId),
    refetchInterval: 5_000,
  })
}

/**
 * Fetches a single connector.
 */
export function useConnector(chargerId: string, connectorId: number) {
  return useQuery({
    queryKey: chargerKeys.connector(chargerId, connectorId),
    queryFn: () => getConnector(chargerId, connectorId),
    enabled: Boolean(chargerId) && connectorId > 0,
    refetchInterval: 5_000,
  })
}

/**
 * Fetches all active transactions for a charger.
 */
export function useTransactions(chargerId: string) {
  return useQuery({
    queryKey: chargerKeys.transactions(chargerId),
    queryFn: () => getTransactions(chargerId),
    enabled: Boolean(chargerId),
    refetchInterval: 5_000,
  })
}
