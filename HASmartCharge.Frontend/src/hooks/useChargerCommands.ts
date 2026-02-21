import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  clearCache,
  getDiagnostics,
  resetCharger,
  setAvailability,
  startTransaction,
  stopTransaction,
  triggerMessage,
  unlockConnector,
} from '@/api/chargersApi'
import type {
  AvailabilityType,
  GetDiagnosticsRequest,
  ResetType,
  TriggerMessageRequest,
} from '@/types/charger'
import { chargerKeys } from './useChargers'

// ---------------------------------------------------------------------------
// Helper — invalidate charger detail + list after any command that may change
// charger state (e.g. start/stop transaction, availability change)
// ---------------------------------------------------------------------------

function useChargerInvalidation(chargerId: string) {
  const queryClient = useQueryClient()
  return () => {
    void queryClient.invalidateQueries({ queryKey: chargerKeys.detail(chargerId) })
    void queryClient.invalidateQueries({ queryKey: chargerKeys.connectors(chargerId) })
    void queryClient.invalidateQueries({ queryKey: chargerKeys.transactions(chargerId) })
    void queryClient.invalidateQueries({ queryKey: chargerKeys.list() })
  }
}

// ---------------------------------------------------------------------------
// Charger-level commands
// ---------------------------------------------------------------------------

export function useResetCharger(chargerId: string) {
  const invalidate = useChargerInvalidation(chargerId)
  return useMutation({
    mutationFn: (type: ResetType) => resetCharger(chargerId, { type }),
    onSuccess: invalidate,
  })
}

export function useClearCache(chargerId: string) {
  const invalidate = useChargerInvalidation(chargerId)
  return useMutation({
    mutationFn: () => clearCache(chargerId),
    onSuccess: invalidate,
  })
}

export function useTriggerMessage(chargerId: string) {
  return useMutation({
    mutationFn: (request: TriggerMessageRequest) => triggerMessage(chargerId, request),
  })
}

export function useGetDiagnostics(chargerId: string) {
  return useMutation({
    mutationFn: (request: GetDiagnosticsRequest) => getDiagnostics(chargerId, request),
  })
}

// ---------------------------------------------------------------------------
// Connector-level commands
// ---------------------------------------------------------------------------

export function useSetAvailability(chargerId: string) {
  const invalidate = useChargerInvalidation(chargerId)
  return useMutation({
    mutationFn: ({ connectorId, type }: { connectorId: number; type: AvailabilityType }) =>
      setAvailability(chargerId, connectorId, { type }),
    onSuccess: invalidate,
  })
}

export function useUnlockConnector(chargerId: string) {
  return useMutation({
    mutationFn: (connectorId: number) => unlockConnector(chargerId, connectorId),
  })
}

export function useStartTransaction(chargerId: string) {
  const invalidate = useChargerInvalidation(chargerId)
  return useMutation({
    mutationFn: ({ connectorId, idTag }: { connectorId: number; idTag: string }) =>
      startTransaction(chargerId, connectorId, { idTag }),
    onSuccess: invalidate,
  })
}

export function useStopTransaction(chargerId: string) {
  const invalidate = useChargerInvalidation(chargerId)
  return useMutation({
    mutationFn: ({
      connectorId,
      transactionId,
    }: {
      connectorId: number
      transactionId: number
    }) => stopTransaction(chargerId, connectorId, transactionId),
    onSuccess: invalidate,
  })
}
