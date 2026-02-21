// ── Connector OCPP status ─────────────────────────────────────────────────────

export type ConnectorStatus =
  | 'Available'
  | 'Preparing'
  | 'Charging'
  | 'SuspendedEVSE'
  | 'SuspendedEV'
  | 'Finishing'
  | 'Reserved'
  | 'Unavailable'
  | 'Faulted'
  | 'Unknown'

// ── Measurands ────────────────────────────────────────────────────────────────

export interface MeasurandValue {
  value: string
  unit: string | null
  context: string | null
}

export interface ConnectorMeasurands {
  connectorId: number
  lastUpdated: string
  energyActiveImportRegister: MeasurandValue | null
  energyReactiveImportRegister: MeasurandValue | null
  energyActiveExportRegister: MeasurandValue | null
  energyReactiveExportRegister: MeasurandValue | null
  powerActiveImport: MeasurandValue | null
  powerReactiveImport: MeasurandValue | null
  powerOffered: MeasurandValue | null
  voltageL1: MeasurandValue | null
  voltageL2: MeasurandValue | null
  voltageL3: MeasurandValue | null
  voltageL1N: MeasurandValue | null
  voltageL2N: MeasurandValue | null
  voltageL3N: MeasurandValue | null
  currentImportL1: MeasurandValue | null
  currentImportL2: MeasurandValue | null
  currentImportL3: MeasurandValue | null
  currentExportL1: MeasurandValue | null
  currentExportL2: MeasurandValue | null
  currentExportL3: MeasurandValue | null
  currentOffered: MeasurandValue | null
  temperature: MeasurandValue | null
  soC: MeasurandValue | null
  frequency: MeasurandValue | null
  rpm: MeasurandValue | null
}

// ── Connector detail ──────────────────────────────────────────────────────────

export interface ConnectorDetail {
  connectorId: number
  status: ConnectorStatus | null
  errorCode: string | null
  info: string | null
  vendorId: string | null
  vendorErrorCode: string | null
  lastStatusUpdate: string | null
  activeTransactionId: number | null
  transactionStartTime: string | null
  idTag: string | null
  measurands: ConnectorMeasurands | null
}

// ── Charger hardware info ─────────────────────────────────────────────────────

export interface ChargerInfo {
  vendor: string | null
  model: string | null
  serialNumber: string | null
  firmwareVersion: string | null
  iccid: string | null
  imsi: string | null
  meterType: string | null
  meterSerialNumber: string | null
}

// ── API response shapes ───────────────────────────────────────────────────────

/** Returned by GET /api/chargers */
export interface ChargerSummary {
  chargePointId: string
  isConnected: boolean
  connectedAt: string | null
  disconnectedAt: string | null
  lastUpdated: string | null
  vendor: string | null
  model: string | null
  firmwareVersion: string | null
  connectorCount: number
}

export interface ChargersListResponse {
  count: number
  chargers: ChargerSummary[]
}

/** Returned by GET /api/chargers/:id */
export interface ChargerDetail {
  chargePointId: string
  isConnected: boolean
  connectedAt: string | null
  disconnectedAt: string | null
  lastUpdated: string | null
  info: ChargerInfo | null
  connectors: ConnectorDetail[]
}

/** Returned by GET /api/chargers/:id/connectors */
export interface ConnectorsListResponse {
  chargerId: string
  count: number
  connectors: ConnectorDetail[]
}

/** A single active-transaction entry from GET /api/chargers/:id/transactions */
export interface ActiveTransaction {
  connectorId: number
  transactionId: number
  idTag: string | null
  startTime: string | null
  connectorStatus: ConnectorStatus | null
  energyActiveImportWh: MeasurandValue | null
}

export interface TransactionsListResponse {
  chargerId: string
  count: number
  transactions: ActiveTransaction[]
}

// ── Command request / response types ─────────────────────────────────────────

export type ResetType = 'Hard' | 'Soft'
export type AvailabilityType = 'Operative' | 'Inoperative'

export interface ResetRequest {
  type: ResetType
}

export interface TriggerMessageRequest {
  requestedMessage: string
  connectorId?: number
}

export interface GetDiagnosticsRequest {
  location: string
  retries?: number
  retryInterval?: number
  startTime?: string
  stopTime?: string
}

export interface SetAvailabilityRequest {
  type: AvailabilityType
}

export interface StartTransactionRequest {
  idTag: string
}

export interface CommandResponse {
  dispatched: boolean
  chargerId: string
  [key: string]: unknown
}
