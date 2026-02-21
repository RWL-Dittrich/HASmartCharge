import { useState } from 'react'
import { Eye, RefreshCw, Search, SlidersHorizontal, Plus, MapPin, StopCircle } from 'lucide-react'
import { TopBar } from '@/components/layout/TopBar'
import { StatCard } from '@/components/ui/StatCard'

// ── Types ─────────────────────────────────────────────────────────────────────

type ChargerStatus = 'Available' | 'Charging' | 'Faulted' | 'Finishing'

interface Connector {
  label: string
  variant: 'default' | 'blue'
}

interface Charger {
  id: string
  model: string
  location: string
  connectors: Connector[]
  firmware: string
  firmwareVariant: 'stable' | 'beta'
  status: ChargerStatus
  fault?: string
}

// ── Static data ───────────────────────────────────────────────────────────────

const CHARGERS: Charger[] = [
  {
    id: 'CP-EU-8821',
    model: 'ABB TERRA S4',
    location: 'Berlin Hub 04',
    connectors: [{ label: 'CCS2', variant: 'default' }, { label: 'CHAdeMO', variant: 'default' }],
    firmware: 'v2.1.4-LTS',
    firmwareVariant: 'stable',
    status: 'Available',
  },
  {
    id: 'CP-US-1029',
    model: 'CHARGEPOINT EXPRESS',
    location: 'LAX Term 1 Parking',
    connectors: [{ label: 'Type 1', variant: 'blue' }, { label: 'CCS1', variant: 'default' }],
    firmware: 'v1.9.8-BETA',
    firmwareVariant: 'beta',
    status: 'Charging',
  },
  {
    id: 'CP-UK-4412',
    model: '',
    location: 'London East Mall',
    connectors: [{ label: 'Type 2', variant: 'default' }],
    firmware: 'v2.1.2-LTS',
    firmwareVariant: 'stable',
    status: 'Faulted',
    fault: 'GROUND FAULT DETECTED',
  },
  {
    id: 'CP-FR-2900',
    model: 'SCHNEIDER EVLINK',
    location: 'Paris Gare du Nord',
    connectors: [{ label: 'Type 2', variant: 'default' }],
    firmware: 'v2.1.4-LTS',
    firmwareVariant: 'stable',
    status: 'Finishing',
  },
]

// ── Status helpers ─────────────────────────────────────────────────────────────

const STATUS_DOT: Record<ChargerStatus, string> = {
  Available: 'bg-emerald-400',
  Charging: 'bg-blue-400',
  Faulted: 'bg-red-400',
  Finishing: 'bg-yellow-400',
}

const STATUS_TEXT: Record<ChargerStatus, string> = {
  Available: 'text-emerald-400',
  Charging: 'text-blue-400',
  Faulted: 'text-red-400',
  Finishing: 'text-yellow-400',
}

const FILTER_TABS: Array<ChargerStatus | 'All'> = ['All', 'Available', 'Charging', 'Faulted']

// ── Sub-components ────────────────────────────────────────────────────────────

function ConnectorBadge({ connector }: { connector: Connector }) {
  return (
    <span
      className={
        connector.variant === 'blue'
          ? 'inline-flex items-center rounded px-1.5 py-0.5 text-[10px] font-semibold bg-blue-500/20 text-blue-300 ring-1 ring-blue-500/30'
          : 'inline-flex items-center rounded px-1.5 py-0.5 text-[10px] font-semibold bg-[#2a3042] text-[#8892a4] ring-1 ring-[#3a4155]'
      }
    >
      {connector.label}
    </span>
  )
}

function FirmwareBadge({ version, variant }: { version: string; variant: 'stable' | 'beta' }) {
  return (
    <span
      className={
        variant === 'beta'
          ? 'text-xs font-mono font-semibold text-amber-400'
          : 'text-xs font-mono font-semibold text-emerald-400'
      }
    >
      {version}
    </span>
  )
}

function ActionButtons({ status }: { status: ChargerStatus }) {
  return (
    <div className="flex items-center gap-2">
      {status === 'Faulted' ? (
        <button className="rounded-md p-1.5 text-red-400 hover:bg-red-400/10 transition-colors">
          <StopCircle className="h-4 w-4" />
        </button>
      ) : (
        <button className="rounded-md p-1.5 text-[#8892a4] hover:bg-[#232938] hover:text-white transition-colors">
          <Eye className="h-4 w-4" />
        </button>
      )}
      <button className="rounded-md p-1.5 text-[#8892a4] hover:bg-[#232938] hover:text-white transition-colors">
        <RefreshCw className="h-4 w-4" />
      </button>
    </div>
  )
}

// ── Page ───────────────────────────────────────────────────────────────────────

export function ChargersPage() {
  const [search, setSearch] = useState('')
  const [activeFilter, setActiveFilter] = useState<ChargerStatus | 'All'>('All')

  const filtered = CHARGERS.filter((c) => {
    const matchesStatus = activeFilter === 'All' || c.status === activeFilter
    const q = search.toLowerCase()
    const matchesSearch =
      !q ||
      c.id.toLowerCase().includes(q) ||
      c.location.toLowerCase().includes(q) ||
      c.firmware.toLowerCase().includes(q)
    return matchesStatus && matchesSearch
  })

  return (
    <div className="flex flex-col h-full overflow-auto">
      <TopBar
        title="Charging Points"
        subtitle="Real-time status of all active infrastructure points"
        actions={
          <button className="flex items-center gap-1.5 rounded-md bg-blue-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-blue-500 transition-colors">
            <Plus className="h-3.5 w-3.5" />
            Provision Charger
          </button>
        }
      />

      <div className="flex-1 p-6 space-y-5">
        {/* Stat cards */}
        <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
          <StatCard title="Total Chargers" value="1,284" change="+2.4%" changePositive />
          <StatCard title="Available Now" value="852" change="+5.1%" changePositive />
          <StatCard title="Active Sessions" value="312" change="Peak: 450" changePositive />
          <StatCard title="System Faults" value="12" change="Action Required" changePositive={false} />
        </div>

        {/* Toolbar */}
        <div className="flex flex-wrap items-center gap-3">
          {/* Search */}
          <div className="relative flex-1 min-w-[220px]">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-[#8892a4]" />
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search by Charger ID, Location, or Firmware..."
              className="w-full rounded-md bg-[#1a1f2e] border border-[#2a3042] pl-8 pr-3 py-1.5 text-xs text-white placeholder-[#8892a4] outline-none focus:border-blue-500 transition-colors"
            />
          </div>

          {/* Status filter tabs */}
          <div className="flex items-center gap-1 rounded-md bg-[#1a1f2e] border border-[#2a3042] p-0.5">
            {FILTER_TABS.map((tab) => (
              <button
                key={tab}
                onClick={() => setActiveFilter(tab)}
                className={`rounded px-3 py-1 text-xs font-medium transition-colors ${activeFilter === tab
                    ? 'bg-blue-600 text-white'
                    : 'text-[#8892a4] hover:text-white'
                  }`}
              >
                {tab === 'All' ? 'All Statuses' : tab}
              </button>
            ))}
          </div>

          {/* More filters */}
          <button className="flex items-center gap-1.5 rounded-md border border-[#2a3042] bg-[#1a1f2e] px-3 py-1.5 text-xs text-[#8892a4] hover:text-white hover:border-[#3a4155] transition-colors">
            <SlidersHorizontal className="h-3.5 w-3.5" />
            More Filters
          </button>
        </div>

        {/* Table */}
        <div className="rounded-lg bg-[#1a1f2e] border border-[#2a3042] overflow-hidden">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-[#2a3042]">
                {['Status', 'Charger ID', 'Location', 'Connector', 'Firmware', 'Actions'].map((col) => (
                  <th key={col} className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wider text-[#8892a4]">
                    {col}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {filtered.map((charger) => (
                <tr
                  key={charger.id}
                  className="border-b border-[#2a3042] last:border-0 hover:bg-[#232938] transition-colors"
                >
                  {/* Status */}
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-2">
                      <span className={`h-2 w-2 rounded-full ${STATUS_DOT[charger.status]}`} />
                      <span className={`text-xs font-semibold ${STATUS_TEXT[charger.status]}`}>
                        {charger.status}
                      </span>
                    </div>
                    {charger.fault && (
                      <p className="mt-0.5 text-[10px] font-semibold text-red-400 tracking-wide">
                        {charger.fault}
                      </p>
                    )}
                  </td>

                  {/* Charger ID */}
                  <td className="px-4 py-3">
                    <div className="font-mono text-xs font-semibold text-white">{charger.id}</div>
                    {charger.model && (
                      <div className="mt-0.5 text-[10px] text-[#8892a4] uppercase tracking-wide">
                        Model: {charger.model}
                      </div>
                    )}
                  </td>

                  {/* Location */}
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-1.5 text-xs text-[#8892a4]">
                      <MapPin className="h-3 w-3 shrink-0" />
                      {charger.location}
                    </div>
                  </td>

                  {/* Connectors */}
                  <td className="px-4 py-3">
                    <div className="flex flex-wrap gap-1">
                      {charger.connectors.map((c) => (
                        <ConnectorBadge key={c.label} connector={c} />
                      ))}
                    </div>
                  </td>

                  {/* Firmware */}
                  <td className="px-4 py-3">
                    <FirmwareBadge version={charger.firmware} variant={charger.firmwareVariant} />
                  </td>

                  {/* Actions */}
                  <td className="px-4 py-3">
                    <ActionButtons status={charger.status} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {/* Pagination */}
          <div className="flex items-center justify-between border-t border-[#2a3042] px-4 py-3">
            <p className="text-xs text-[#8892a4]">Showing 1 to 10 of 1,284 entries</p>
            <div className="flex items-center gap-1">
              {['‹', '1', '2', '3', '…', '129', '›'].map((page, i) => (
                <button
                  key={i}
                  className={`min-w-[28px] rounded px-2 py-1 text-xs font-medium transition-colors ${page === '1'
                      ? 'bg-blue-600 text-white'
                      : 'text-[#8892a4] hover:bg-[#232938] hover:text-white'
                    }`}
                >
                  {page}
                </button>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}


