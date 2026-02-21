import { useState } from 'react'
import { X, Wifi, WifiOff, Cpu, Zap, ArrowDownToLine } from 'lucide-react'
import { useCharger, useTransactions } from '@/hooks/useChargers'
import { ChargerStatusBadge } from './ChargerStatusBadge'
import { ConnectorCard } from './ConnectorCard'

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

type DrawerTab = 'info' | 'connectors' | 'transactions'

interface ChargerDetailDrawerProps {
  chargerId: string | null
  onClose: () => void
}

// ---------------------------------------------------------------------------
// Info row helper
// ---------------------------------------------------------------------------

function InfoRow({ label, value }: { label: string; value: string | null | undefined }) {
  if (!value) return null
  return (
    <div className="flex items-start justify-between gap-4 py-2 border-b border-[#2a3042] last:border-0">
      <span className="text-xs text-[#8892a4] shrink-0">{label}</span>
      <span className="text-xs font-mono text-right text-white break-all">{value}</span>
    </div>
  )
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export function ChargerDetailDrawer({ chargerId, onClose }: ChargerDetailDrawerProps) {
  const [tab, setTab] = useState<DrawerTab>('info')

  const {
    data: charger,
    isLoading: chargerLoading,
    isError: chargerError,
  } = useCharger(chargerId ?? '')

  const {
    data: txData,
    isLoading: txLoading,
  } = useTransactions(chargerId ?? '')

  const isOpen = Boolean(chargerId)

  return (
    <>
      {/* Backdrop */}
      <div
        onClick={onClose}
        className={`fixed inset-0 z-30 bg-black/40 backdrop-blur-sm transition-opacity duration-200 ${isOpen ? 'opacity-100' : 'opacity-0 pointer-events-none'
          }`}
        aria-hidden
      />

      {/* Panel */}
      <div
        className={`fixed right-0 top-0 z-40 h-full w-full max-w-[480px] bg-[#141928] border-l border-[#2a3042] shadow-2xl flex flex-col transition-transform duration-200 ${isOpen ? 'translate-x-0' : 'translate-x-full'
          }`}
      >
        {/* Header */}
        <div className="flex items-start justify-between gap-4 border-b border-[#2a3042] px-5 py-4">
          <div>
            <div className="flex items-center gap-2.5">
              <Cpu className="h-4 w-4 text-[#8892a4]" />
              <span className="font-mono text-sm font-semibold text-white">
                {chargerId ?? '—'}
              </span>
            </div>
            {charger && (
              <div className="mt-1.5 flex items-center gap-2">
                <ChargerStatusBadge isConnected={charger.isConnected} />
                {charger.info?.vendor && (
                  <span className="text-[11px] text-[#8892a4]">
                    {charger.info.vendor}{charger.info.model ? ` · ${charger.info.model}` : ''}
                  </span>
                )}
              </div>
            )}
          </div>
          <button
            onClick={onClose}
            className="rounded-md p-1.5 text-[#8892a4] hover:bg-[#232938] hover:text-white transition-colors"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {/* Tabs */}
        <div className="flex border-b border-[#2a3042] px-5 gap-1">
          {(['info', 'connectors', 'transactions'] as DrawerTab[]).map((t) => (
            <button
              key={t}
              onClick={() => setTab(t)}
              className={`px-3 py-2.5 text-xs font-medium capitalize transition-colors border-b-2 -mb-px ${tab === t
                  ? 'border-blue-500 text-white'
                  : 'border-transparent text-[#8892a4] hover:text-white'
                }`}
            >
              {t}
              {t === 'connectors' && charger && (
                <span className="ml-1.5 text-[10px] bg-[#2a3042] text-[#8892a4] rounded px-1 py-0.5">
                  {charger.connectors.length}
                </span>
              )}
              {t === 'transactions' && txData && txData.count > 0 && (
                <span className="ml-1.5 text-[10px] bg-blue-500/20 text-blue-300 rounded px-1 py-0.5">
                  {txData.count}
                </span>
              )}
            </button>
          ))}
        </div>

        {/* Body */}
        <div className="flex-1 overflow-y-auto px-5 py-4">
          {chargerLoading && (
            <div className="flex items-center justify-center py-16 text-xs text-[#8892a4]">
              Loading…
            </div>
          )}
          {chargerError && (
            <div className="flex items-center justify-center py-16 text-xs text-red-400">
              Failed to load charger details.
            </div>
          )}

          {/* ── Info tab ─────────────────────────────────────────────────── */}
          {!chargerLoading && !chargerError && charger && tab === 'info' && (
            <div className="space-y-4">
              {/* Connection info */}
              <section>
                <div className="mb-2 flex items-center gap-2">
                  {charger.isConnected
                    ? <Wifi className="h-3.5 w-3.5 text-emerald-400" />
                    : <WifiOff className="h-3.5 w-3.5 text-[#8892a4]" />}
                  <span className="text-[10px] font-semibold uppercase tracking-wider text-[#8892a4]">
                    Connection
                  </span>
                </div>
                <div className="rounded-lg bg-[#1e2435] border border-[#2a3042] px-4 py-1">
                  <InfoRow
                    label="Connected at"
                    value={charger.connectedAt ? new Date(charger.connectedAt).toLocaleString() : null}
                  />
                  <InfoRow
                    label="Disconnected at"
                    value={
                      charger.disconnectedAt
                        ? new Date(charger.disconnectedAt).toLocaleString()
                        : undefined
                    }
                  />
                  <InfoRow
                    label="Last updated"
                    value={charger.lastUpdated ? new Date(charger.lastUpdated).toLocaleString() : null}
                  />
                </div>
              </section>

              {/* Hardware info */}
              {charger.info && (
                <section>
                  <div className="mb-2 flex items-center gap-2">
                    <Cpu className="h-3.5 w-3.5 text-[#8892a4]" />
                    <span className="text-[10px] font-semibold uppercase tracking-wider text-[#8892a4]">
                      Hardware
                    </span>
                  </div>
                  <div className="rounded-lg bg-[#1e2435] border border-[#2a3042] px-4 py-1">
                    <InfoRow label="Vendor" value={charger.info.vendor} />
                    <InfoRow label="Model" value={charger.info.model} />
                    <InfoRow label="Serial Number" value={charger.info.serialNumber} />
                    <InfoRow label="Firmware" value={charger.info.firmwareVersion} />
                    <InfoRow label="ICCID" value={charger.info.iccid} />
                    <InfoRow label="IMSI" value={charger.info.imsi} />
                    <InfoRow label="Meter Type" value={charger.info.meterType} />
                    <InfoRow label="Meter Serial" value={charger.info.meterSerialNumber} />
                  </div>
                </section>
              )}
            </div>
          )}

          {/* ── Connectors tab ───────────────────────────────────────────── */}
          {!chargerLoading && !chargerError && charger && tab === 'connectors' && (
            <div className="space-y-3">
              {charger.connectors.length === 0 && (
                <p className="py-12 text-center text-xs text-[#8892a4]">No connectors reported.</p>
              )}
              {charger.connectors.map((c) => (
                <ConnectorCard key={c.connectorId} chargerId={charger.chargePointId} connector={c} />
              ))}
            </div>
          )}

          {/* ── Transactions tab ─────────────────────────────────────────── */}
          {tab === 'transactions' && (
            <div className="space-y-2">
              {txLoading && (
                <p className="py-12 text-center text-xs text-[#8892a4]">Loading…</p>
              )}
              {!txLoading && txData?.transactions.length === 0 && (
                <p className="py-12 text-center text-xs text-[#8892a4]">No active transactions.</p>
              )}
              {txData?.transactions.map((tx) => (
                <div
                  key={tx.transactionId}
                  className="rounded-lg bg-[#1e2435] border border-[#2a3042] p-4 space-y-2"
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <Zap className="h-3.5 w-3.5 text-blue-400" />
                      <span className="text-xs font-semibold text-white">
                        Transaction #{tx.transactionId}
                      </span>
                    </div>
                    <span className="text-[11px] text-[#8892a4]">
                      Connector {tx.connectorId}
                    </span>
                  </div>
                  <div className="grid grid-cols-2 gap-x-4 gap-y-1">
                    {tx.idTag && (
                      <>
                        <span className="text-[11px] text-[#8892a4]">ID Tag</span>
                        <span className="text-[11px] font-mono text-white">{tx.idTag}</span>
                      </>
                    )}
                    {tx.startTime && (
                      <>
                        <span className="text-[11px] text-[#8892a4]">Started</span>
                        <span className="text-[11px] font-mono text-white">
                          {new Date(tx.startTime).toLocaleTimeString()}
                        </span>
                      </>
                    )}
                    {tx.energyActiveImportWh && (
                      <>
                        <span className="text-[11px] text-[#8892a4]">Energy</span>
                        <span className="text-[11px] font-mono text-white">
                          {tx.energyActiveImportWh.value} {tx.energyActiveImportWh.unit}
                        </span>
                      </>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Footer hint */}
        <div className="border-t border-[#2a3042] px-5 py-3">
          <p className="flex items-center gap-1.5 text-[11px] text-[#8892a4]">
            <ArrowDownToLine className="h-3 w-3" />
            Data refreshes automatically every 5 s
          </p>
        </div>
      </div>
    </>
  )
}
