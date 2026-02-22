import { useState } from 'react'
import { Unlock, Zap, ZapOff, Play, Square } from 'lucide-react'
import type { ConnectorDetail, MeasurandValue } from '@/types/charger'
import { ConnectorStatusBadge } from './ConnectorStatusBadge'
import {
  useSetAvailability,
  useStartTransaction,
  useStopTransaction,
  useUnlockConnector,
} from '@/hooks/useChargerCommands'

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function fmt(m: MeasurandValue | null | undefined, decimals = 1): string {
  if (!m) return '—'
  const num = parseFloat(m.value)
  return `${isNaN(num) ? m.value : num.toFixed(decimals)}${m.unit ? ` ${m.unit}` : ''}`
}

function MeasurandRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between">
      <span className="text-[11px] text-[#8892a4]">{label}</span>
      <span className="text-[11px] font-mono text-white">{value}</span>
    </div>
  )
}

// Ordered display config for all possible measurands.
// Only entries whose value is non-null will be rendered.
type MeasurandKey = Exclude<keyof import('@/types/charger').ConnectorMeasurands, 'connectorId' | 'lastUpdated'>

const MEASURAND_CONFIG: { key: MeasurandKey; label: string; decimals?: number }[] = [
  { key: 'energyActiveImportRegister', label: 'Energy (Import)' },
  { key: 'energyActiveExportRegister', label: 'Energy (Export)' },
  { key: 'energyReactiveImportRegister', label: 'Energy Reactive (Import)' },
  { key: 'energyReactiveExportRegister', label: 'Energy Reactive (Export)' },
  { key: 'powerActiveImport', label: 'Power (Import)' },
  { key: 'powerReactiveImport', label: 'Power Reactive (Import)' },
  { key: 'powerOffered', label: 'Power Offered' },
  { key: 'voltageL1', label: 'Voltage L1' },
  { key: 'voltageL2', label: 'Voltage L2' },
  { key: 'voltageL3', label: 'Voltage L3' },
  { key: 'voltageL1N', label: 'Voltage L1-N' },
  { key: 'voltageL2N', label: 'Voltage L2-N' },
  { key: 'voltageL3N', label: 'Voltage L3-N' },
  { key: 'currentImportL1', label: 'Current Import L1' },
  { key: 'currentImportL2', label: 'Current Import L2' },
  { key: 'currentImportL3', label: 'Current Import L3' },
  { key: 'currentExportL1', label: 'Current Export L1' },
  { key: 'currentExportL2', label: 'Current Export L2' },
  { key: 'currentExportL3', label: 'Current Export L3' },
  { key: 'currentOffered', label: 'Current Offered' },
  { key: 'soC', label: 'SoC', decimals: 0 },
  { key: 'temperature', label: 'Temperature', decimals: 0 },
  { key: 'frequency', label: 'Frequency' },
  { key: 'rpm', label: 'RPM', decimals: 0 },
]

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

interface ConnectorCardProps {
  chargerId: string
  connector: ConnectorDetail
}

export function ConnectorCard({ chargerId, connector }: ConnectorCardProps) {
  const [idTagInput, setIdTagInput] = useState('')
  const [showStartForm, setShowStartForm] = useState(false)

  const unlock = useUnlockConnector(chargerId)
  const setAvail = useSetAvailability(chargerId)
  const startTx = useStartTransaction(chargerId)
  const stopTx = useStopTransaction(chargerId)

  const { connectorId, status, errorCode, measurands } = connector
  const isFaulted = status === 'Faulted'
  const isUnavailable = status === 'Unavailable'
  const hasTransaction = connector.activeTransactionId !== null

  function handleStartTransaction() {
    if (!idTagInput.trim()) return
    startTx.mutate(
      { connectorId, idTag: idTagInput.trim() },
      { onSuccess: () => { setIdTagInput(''); setShowStartForm(false) } },
    )
  }

  return (
    <div className="rounded-lg bg-[#232938] border border-[#2a3042] p-4 space-y-3">
      {/* Header */}
      <div className="flex items-center justify-between">
        <span className="text-xs font-semibold text-[#8892a4] uppercase tracking-wider">
          Connector {connectorId}
        </span>
        <ConnectorStatusBadge status={status} />
      </div>

      {/* Error code */}
      {errorCode && errorCode !== 'NoError' && (
        <p className="text-[11px] font-semibold text-red-400 tracking-wide">
          Error: {errorCode}
        </p>
      )}

      {/* Active transaction info */}
      {hasTransaction && (
        <div className="rounded bg-blue-500/10 border border-blue-500/20 p-2 space-y-0.5">
          <div className="flex items-center justify-between">
            <span className="text-[11px] text-[#8892a4]">Transaction</span>
            <span className="text-[11px] font-mono text-white">#{connector.activeTransactionId}</span>
          </div>
          {connector.idTag && (
            <div className="flex items-center justify-between">
              <span className="text-[11px] text-[#8892a4]">ID Tag</span>
              <span className="text-[11px] font-mono text-white">{connector.idTag}</span>
            </div>
          )}
          {connector.transactionStartTime && (
            <div className="flex items-center justify-between">
              <span className="text-[11px] text-[#8892a4]">Started</span>
              <span className="text-[11px] font-mono text-white">
                {new Date(connector.transactionStartTime).toLocaleTimeString()}
              </span>
            </div>
          )}
        </div>
      )}

      {/* Measurands — only render rows that have a non-null value */}
      {measurands && MEASURAND_CONFIG.some(({ key }) => measurands[key] !== null) && (
        <div className="space-y-1">
          {MEASURAND_CONFIG.filter(({ key }) => measurands[key] !== null).map(({ key, label, decimals }) => (
            <MeasurandRow key={key} label={label} value={fmt(measurands[key], decimals)} />
          ))}
        </div>
      )}

      {/* Actions */}
      <div className="flex flex-wrap gap-2 pt-1">
        {/* Unlock */}
        <button
          onClick={() => unlock.mutate(connectorId)}
          disabled={unlock.isPending}
          className="flex items-center gap-1 rounded px-2 py-1 text-[11px] font-medium text-[#8892a4] border border-[#2a3042] hover:text-white hover:border-[#3a4155] transition-colors disabled:opacity-50"
        >
          <Unlock className="h-3 w-3" />
          {unlock.isPending ? 'Unlocking…' : 'Unlock'}
        </button>

        {/* Set Operative / Inoperative */}
        {isUnavailable ? (
          <button
            onClick={() => setAvail.mutate({ connectorId, type: 'Operative' })}
            disabled={setAvail.isPending}
            className="flex items-center gap-1 rounded px-2 py-1 text-[11px] font-medium text-emerald-400 border border-emerald-500/30 hover:bg-emerald-500/10 transition-colors disabled:opacity-50"
          >
            <Zap className="h-3 w-3" />
            Set Operative
          </button>
        ) : (
          <button
            onClick={() => setAvail.mutate({ connectorId, type: 'Inoperative' })}
            disabled={setAvail.isPending}
            className="flex items-center gap-1 rounded px-2 py-1 text-[11px] font-medium text-[#8892a4] border border-[#2a3042] hover:text-amber-400 hover:border-amber-500/30 transition-colors disabled:opacity-50"
          >
            <ZapOff className="h-3 w-3" />
            Set Inoperative
          </button>
        )}

        {/* Stop transaction */}
        {hasTransaction && (
          <button
            onClick={() => stopTx.mutate({ connectorId, transactionId: connector.activeTransactionId! })}
            disabled={stopTx.isPending}
            className="flex items-center gap-1 rounded px-2 py-1 text-[11px] font-medium text-red-400 border border-red-500/30 hover:bg-red-500/10 transition-colors disabled:opacity-50"
          >
            <Square className="h-3 w-3" />
            {stopTx.isPending ? 'Stopping…' : 'Stop Transaction'}
          </button>
        )}

        {/* Start transaction */}
        {!hasTransaction && !isFaulted && !isUnavailable && (
          <button
            onClick={() => setShowStartForm((v) => !v)}
            className="flex items-center gap-1 rounded px-2 py-1 text-[11px] font-medium text-blue-400 border border-blue-500/30 hover:bg-blue-500/10 transition-colors"
          >
            <Play className="h-3 w-3" />
            Start Transaction
          </button>
        )}
      </div>

      {/* Start transaction form */}
      {showStartForm && (
        <div className="flex gap-2 pt-1">
          <input
            value={idTagInput}
            onChange={(e) => setIdTagInput(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleStartTransaction()}
            placeholder="ID Tag..."
            className="flex-1 rounded bg-[#1a1f2e] border border-[#2a3042] px-2 py-1 text-xs text-white placeholder-[#8892a4] outline-none focus:border-blue-500 transition-colors"
          />
          <button
            onClick={handleStartTransaction}
            disabled={startTx.isPending || !idTagInput.trim()}
            className="rounded px-2 py-1 text-xs font-medium bg-blue-600 text-white hover:bg-blue-500 transition-colors disabled:opacity-50"
          >
            {startTx.isPending ? '…' : 'Go'}
          </button>
        </div>
      )}

      {/* Command feedback */}
      {(unlock.isError || setAvail.isError || startTx.isError || stopTx.isError) && (
        <p className="text-[11px] text-red-400">
          {(
            (unlock.error ?? setAvail.error ?? startTx.error ?? stopTx.error) as Error
          )?.message ?? 'Command failed'}
        </p>
      )}
    </div>
  )
}
