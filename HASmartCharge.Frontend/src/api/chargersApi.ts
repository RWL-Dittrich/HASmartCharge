import type {
  ChargersListResponse,
  ChargerDetail,
  ConnectorsListResponse,
  ConnectorDetail,
  TransactionsListResponse,
  CommandResponse,
  ResetRequest,
  TriggerMessageRequest,
  GetDiagnosticsRequest,
  SetAvailabilityRequest,
  StartTransactionRequest,
} from '@/types/charger'

// ---------------------------------------------------------------------------
// Internal helpers
// ---------------------------------------------------------------------------

async function apiFetch<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    headers: { 'Content-Type': 'application/json', ...init?.headers },
    ...init,
  })

  if (!response.ok) {
    let message = `HTTP ${response.status}`
    try {
      const body = await response.json()
      message = body?.error ?? message
    } catch {
      // ignore parse failure
    }

    if (response.status === 404) throw new ApiError(404, message)
    if (response.status === 503) throw new ApiError(503, `Charger offline — ${message}`)
    throw new ApiError(response.status, message)
  }

  return response.json() as Promise<T>
}

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

// ---------------------------------------------------------------------------
// Read endpoints
// ---------------------------------------------------------------------------

export function getChargers(connected?: boolean): Promise<ChargersListResponse> {
  const params = connected !== undefined ? `?connected=${connected}` : ''
  return apiFetch<ChargersListResponse>(`/api/chargers${params}`)
}

export function getCharger(chargerId: string): Promise<ChargerDetail> {
  return apiFetch<ChargerDetail>(`/api/chargers/${encodeURIComponent(chargerId)}`)
}

export function getConnectors(chargerId: string): Promise<ConnectorsListResponse> {
  return apiFetch<ConnectorsListResponse>(
    `/api/chargers/${encodeURIComponent(chargerId)}/connectors`,
  )
}

export function getConnector(chargerId: string, connectorId: number): Promise<ConnectorDetail> {
  return apiFetch<ConnectorDetail>(
    `/api/chargers/${encodeURIComponent(chargerId)}/connectors/${connectorId}`,
  )
}

export function getTransactions(chargerId: string): Promise<TransactionsListResponse> {
  return apiFetch<TransactionsListResponse>(
    `/api/chargers/${encodeURIComponent(chargerId)}/transactions`,
  )
}

// ---------------------------------------------------------------------------
// Charger-level commands
// ---------------------------------------------------------------------------

export function resetCharger(chargerId: string, request: ResetRequest): Promise<CommandResponse> {
  return apiFetch<CommandResponse>(
    `/api/chargers/${encodeURIComponent(chargerId)}/reset`,
    { method: 'POST', body: JSON.stringify(request) },
  )
}

export function clearCache(chargerId: string): Promise<CommandResponse> {
  return apiFetch<CommandResponse>(
    `/api/chargers/${encodeURIComponent(chargerId)}/clear-cache`,
    { method: 'POST', body: JSON.stringify({}) },
  )
}

export function triggerMessage(
  chargerId: string,
  request: TriggerMessageRequest,
): Promise<CommandResponse> {
  return apiFetch<CommandResponse>(
    `/api/chargers/${encodeURIComponent(chargerId)}/trigger-message`,
    { method: 'POST', body: JSON.stringify(request) },
  )
}

export function getDiagnostics(
  chargerId: string,
  request: GetDiagnosticsRequest,
): Promise<CommandResponse> {
  return apiFetch<CommandResponse>(
    `/api/chargers/${encodeURIComponent(chargerId)}/diagnostics`,
    { method: 'POST', body: JSON.stringify(request) },
  )
}

// ---------------------------------------------------------------------------
// Connector-level commands
// ---------------------------------------------------------------------------

export function setAvailability(
  chargerId: string,
  connectorId: number,
  request: SetAvailabilityRequest,
): Promise<CommandResponse> {
  return apiFetch<CommandResponse>(
    `/api/chargers/${encodeURIComponent(chargerId)}/connectors/${connectorId}/availability`,
    { method: 'PUT', body: JSON.stringify(request) },
  )
}

export function unlockConnector(
  chargerId: string,
  connectorId: number,
): Promise<CommandResponse> {
  return apiFetch<CommandResponse>(
    `/api/chargers/${encodeURIComponent(chargerId)}/connectors/${connectorId}/unlock`,
    { method: 'POST', body: JSON.stringify({}) },
  )
}

export function startTransaction(
  chargerId: string,
  connectorId: number,
  request: StartTransactionRequest,
): Promise<CommandResponse> {
  return apiFetch<CommandResponse>(
    `/api/chargers/${encodeURIComponent(chargerId)}/connectors/${connectorId}/transactions`,
    { method: 'POST', body: JSON.stringify(request) },
  )
}

export function stopTransaction(
  chargerId: string,
  connectorId: number,
  transactionId: number,
): Promise<CommandResponse> {
  return apiFetch<CommandResponse>(
    `/api/chargers/${encodeURIComponent(chargerId)}/connectors/${connectorId}/transactions/${transactionId}`,
    { method: 'DELETE' },
  )
}
