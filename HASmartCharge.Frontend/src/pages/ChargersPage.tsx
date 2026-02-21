import { useMemo, useState } from 'react'
import { Eye, Search, SlidersHorizontal, Plus, RefreshCw } from 'lucide-react'
import { TopBar } from '@/components/layout/TopBar'
import { StatCard } from '@/components/ui/StatCard'
import { ChargerStatusBadge } from '@/components/chargers/ChargerStatusBadge'
import { ChargerCommandsMenu } from '@/components/chargers/ChargerCommandsMenu'
import { ChargerDetailDrawer } from '@/components/chargers/ChargerDetailDrawer'
import { useChargers } from '@/hooks/useChargers'
import type { ChargerSummary } from '@/types/charger'

// ── Constants ─────────────────────────────────────────────────────────────────

const PAGE_SIZE = 10

type ConnectionFilter = 'All' | 'Online' | 'Offline'
const FILTER_TABS: ConnectionFilter[] = ['All', 'Online', 'Offline']

// ── Helpers ───────────────────────────────────────────────────────────────────

function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '—'
  return new Date(iso).toLocaleString(undefined, {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

// ── Skeleton rows ─────────────────────────────────────────────────────────────

function SkeletonRows() {
  return (
    <>
      {Array.from({ length: 5 }).map((_, i) => (
        <tr key={i} className="border-b border-[#2a3042] last:border-0">
          {Array.from({ length: 6 }).map((_, j) => (
            <td key={j} className="px-4 py-3.5">
              <div className="h-3 rounded bg-[#2a3042] animate-pulse w-3/4" />
            </td>
          ))}
        </tr>
      ))}
    </>
  )
}

// ── Page ───────────────────────────────────────────────────────────────────────

export function ChargersPage() {
  const [search, setSearch] = useState('')
  const [activeFilter, setActiveFilter] = useState<ConnectionFilter>('All')
  const [page, setPage] = useState(0)
  const [selectedChargerId, setSelectedChargerId] = useState<string | null>(null)

  const { data, isLoading, isError, refetch, isFetching } = useChargers()

  // ── Derived stats ────────────────────────────────────────────────────────
  const stats = useMemo(() => {
    const chargers = data?.chargers ?? []
    const online = chargers.filter((c) => c.isConnected).length
    const total = chargers.length
    const totalConnectors = chargers.reduce((sum, c) => sum + c.connectorCount, 0)
    return { total, online, offline: total - online, totalConnectors }
  }, [data])

  // ── Filtering ─────────────────────────────────────────────────────────────
  const filtered: ChargerSummary[] = useMemo(() => {
    const chargers = data?.chargers ?? []
    const q = search.trim().toLowerCase()
    return chargers.filter((c) => {
      const matchesFilter =
        activeFilter === 'All' ||
        (activeFilter === 'Online' && c.isConnected) ||
        (activeFilter === 'Offline' && !c.isConnected)
      const matchesSearch =
        !q ||
        c.chargePointId.toLowerCase().includes(q) ||
        (c.vendor ?? '').toLowerCase().includes(q) ||
        (c.model ?? '').toLowerCase().includes(q) ||
        (c.firmwareVersion ?? '').toLowerCase().includes(q)
      return matchesFilter && matchesSearch
    })
  }, [data, search, activeFilter])

  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE))
  const safePage = Math.min(page, totalPages - 1)
  const visible = filtered.slice(safePage * PAGE_SIZE, (safePage + 1) * PAGE_SIZE)

  function handleFilterChange(f: ConnectionFilter) {
    setActiveFilter(f)
    setPage(0)
  }

  function handleSearchChange(value: string) {
    setSearch(value)
    setPage(0)
  }

  // ── Render ───────────────────────────────────────────────────────────────
  return (
    <div className="flex flex-col h-full overflow-auto">
      <TopBar
        title="Charging Points"
        subtitle="Real-time status of all active infrastructure points"
        actions={
          <div className="flex items-center gap-2">
            <button
              onClick={() => void refetch()}
              disabled={isFetching}
              className="rounded-md p-1.5 text-[#8892a4] hover:bg-[#232938] hover:text-white transition-colors disabled:opacity-50"
              title="Refresh"
            >
              <RefreshCw className={`h-3.5 w-3.5 ${isFetching ? 'animate-spin' : ''}`} />
            </button>
            <button className="flex items-center gap-1.5 rounded-md bg-blue-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-blue-500 transition-colors">
              <Plus className="h-3.5 w-3.5" />
              Provision Charger
            </button>
          </div>
        }
      />

      <div className="flex-1 p-6 space-y-5">
        {/* Stat cards */}
        <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
          <StatCard
            title="Total Chargers"
            value={isLoading ? '…' : stats.total.toString()}
          />
          <StatCard
            title="Online Now"
            value={isLoading ? '…' : stats.online.toString()}
            change={stats.total > 0 ? `${Math.round((stats.online / stats.total) * 100)}% connected` : undefined}
            changePositive={stats.online > 0}
          />
          <StatCard
            title="Offline"
            value={isLoading ? '…' : stats.offline.toString()}
            changePositive={false}
          />
          <StatCard
            title="Total Connectors"
            value={isLoading ? '…' : stats.totalConnectors.toString()}
          />
        </div>

        {/* Toolbar */}
        <div className="flex flex-wrap items-center gap-3">
          {/* Search */}
          <div className="relative flex-1 min-w-[220px]">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-[#8892a4]" />
            <input
              value={search}
              onChange={(e) => handleSearchChange(e.target.value)}
              placeholder="Search by Charger ID, Vendor, Model, or Firmware..."
              className="w-full rounded-md bg-[#1a1f2e] border border-[#2a3042] pl-8 pr-3 py-1.5 text-xs text-white placeholder-[#8892a4] outline-none focus:border-blue-500 transition-colors"
            />
          </div>

          {/* Connection filter tabs */}
          <div className="flex items-center gap-1 rounded-md bg-[#1a1f2e] border border-[#2a3042] p-0.5">
            {FILTER_TABS.map((tab) => (
              <button
                key={tab}
                onClick={() => handleFilterChange(tab)}
                className={`rounded px-3 py-1 text-xs font-medium transition-colors ${activeFilter === tab
                    ? 'bg-blue-600 text-white'
                    : 'text-[#8892a4] hover:text-white'
                  }`}
              >
                {tab === 'All' ? 'All Statuses' : tab}
              </button>
            ))}
          </div>

          {/* More filters (placeholder) */}
          <button className="flex items-center gap-1.5 rounded-md border border-[#2a3042] bg-[#1a1f2e] px-3 py-1.5 text-xs text-[#8892a4] hover:text-white hover:border-[#3a4155] transition-colors">
            <SlidersHorizontal className="h-3.5 w-3.5" />
            More Filters
          </button>
        </div>

        {/* Error state */}
        {isError && (
          <div className="rounded-lg bg-red-500/10 border border-red-500/20 px-4 py-3 text-xs text-red-400">
            Failed to load chargers. Check that the backend is running and try refreshing.
          </div>
        )}

        {/* Table */}
        <div className="rounded-lg bg-[#1a1f2e] border border-[#2a3042] overflow-hidden">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-[#2a3042]">
                {['Status', 'Charger ID', 'Last Activity', 'Connectors', 'Firmware', 'Actions'].map(
                  (col) => (
                    <th
                      key={col}
                      className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wider text-[#8892a4]"
                    >
                      {col}
                    </th>
                  ),
                )}
              </tr>
            </thead>
            <tbody>
              {isLoading && <SkeletonRows />}

              {!isLoading && visible.length === 0 && (
                <tr>
                  <td colSpan={6} className="px-4 py-12 text-center text-xs text-[#8892a4]">
                    {data ? 'No chargers match your current filters.' : 'No chargers found.'}
                  </td>
                </tr>
              )}

              {visible.map((charger) => (
                <tr
                  key={charger.chargePointId}
                  className="border-b border-[#2a3042] last:border-0 hover:bg-[#232938] transition-colors cursor-pointer"
                  onClick={() => setSelectedChargerId(charger.chargePointId)}
                >
                  {/* Status */}
                  <td className="px-4 py-3.5">
                    <ChargerStatusBadge isConnected={charger.isConnected} />
                    {charger.isConnected && charger.connectedAt && (
                      <p className="mt-0.5 text-[10px] text-[#8892a4]">
                        since {formatDateTime(charger.connectedAt)}
                      </p>
                    )}
                  </td>

                  {/* Charger ID */}
                  <td className="px-4 py-3.5">
                    <div className="font-mono text-xs font-semibold text-white">
                      {charger.chargePointId}
                    </div>
                    {(charger.vendor || charger.model) && (
                      <div className="mt-0.5 text-[10px] text-[#8892a4] uppercase tracking-wide">
                        {[charger.vendor, charger.model].filter(Boolean).join(' · ')}
                      </div>
                    )}
                  </td>

                  {/* Last Activity */}
                  <td className="px-4 py-3.5 text-xs text-[#8892a4]">
                    {formatDateTime(charger.lastUpdated ?? charger.connectedAt ?? charger.disconnectedAt)}
                  </td>

                  {/* Connectors */}
                  <td className="px-4 py-3.5">
                    <span className="inline-flex items-center rounded px-2 py-0.5 text-[11px] font-semibold bg-[#2a3042] text-[#8892a4] ring-1 ring-[#3a4155]">
                      {charger.connectorCount} connector{charger.connectorCount !== 1 ? 's' : ''}
                    </span>
                  </td>

                  {/* Firmware */}
                  <td className="px-4 py-3.5">
                    {charger.firmwareVersion ? (
                      <span className="text-xs font-mono font-semibold text-emerald-400">
                        {charger.firmwareVersion}
                      </span>
                    ) : (
                      <span className="text-xs text-[#4a5568]">—</span>
                    )}
                  </td>

                  {/* Actions */}
                  <td className="px-4 py-3.5" onClick={(e) => e.stopPropagation()}>
                    <div className="flex items-center gap-1">
                      <button
                        onClick={() => setSelectedChargerId(charger.chargePointId)}
                        className="rounded-md p-1.5 text-[#8892a4] hover:bg-[#232938] hover:text-white transition-colors"
                        title="View details"
                      >
                        <Eye className="h-4 w-4" />
                      </button>
                      <ChargerCommandsMenu
                        chargerId={charger.chargePointId}
                        isConnected={charger.isConnected}
                      />
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {/* Pagination */}
          <div className="flex items-center justify-between border-t border-[#2a3042] px-4 py-3">
            <p className="text-xs text-[#8892a4]">
              {filtered.length === 0
                ? 'No entries'
                : `Showing ${safePage * PAGE_SIZE + 1}–${Math.min(
                  (safePage + 1) * PAGE_SIZE,
                  filtered.length,
                )} of ${filtered.length}`}
            </p>
            <div className="flex items-center gap-1">
              <button
                onClick={() => setPage((p) => Math.max(0, p - 1))}
                disabled={safePage === 0}
                className="min-w-[28px] rounded px-2 py-1 text-xs font-medium text-[#8892a4] hover:bg-[#232938] hover:text-white transition-colors disabled:opacity-30"
              >
                ‹
              </button>
              {Array.from({ length: totalPages }).map((_, i) => (
                <button
                  key={i}
                  onClick={() => setPage(i)}
                  className={`min-w-[28px] rounded px-2 py-1 text-xs font-medium transition-colors ${i === safePage
                      ? 'bg-blue-600 text-white'
                      : 'text-[#8892a4] hover:bg-[#232938] hover:text-white'
                    }`}
                >
                  {i + 1}
                </button>
              ))}
              <button
                onClick={() => setPage((p) => Math.min(totalPages - 1, p + 1))}
                disabled={safePage >= totalPages - 1}
                className="min-w-[28px] rounded px-2 py-1 text-xs font-medium text-[#8892a4] hover:bg-[#232938] hover:text-white transition-colors disabled:opacity-30"
              >
                ›
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* Detail drawer */}
      <ChargerDetailDrawer
        chargerId={selectedChargerId}
        onClose={() => setSelectedChargerId(null)}
      />
    </div>
  )
}

