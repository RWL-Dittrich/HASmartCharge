import { Zap, CheckCircle, Activity, AlertTriangle, BatteryCharging, Loader2 } from 'lucide-react'
import { Link } from 'react-router'
import { TopBar } from '@/components/layout/TopBar'
import { StatCard } from '@/components/ui/StatCard'
import { ConnectorStatusBadge } from '@/components/chargers/ConnectorStatusBadge'
import { useDashboardSummary } from '@/hooks/useDashboard'
import { formatDuration, formatMeasurand } from '@/lib/utils'
import type { ConnectorStatus } from '@/types/charger'

export function DashboardPage() {
  const { data: summary, isLoading, isError } = useDashboardSummary()

  return (
    <div className="flex flex-col h-full overflow-auto">
      <TopBar title="Dashboard" subtitle="Real-time status of your charging infrastructure" />

      <div className="flex-1 p-6 space-y-6">
        {isLoading && (
          <div className="flex items-center justify-center py-20">
            <Loader2 className="h-6 w-6 text-blue-400 animate-spin" />
            <span className="ml-2 text-sm text-[#8892a4]">Loading dashboard…</span>
          </div>
        )}

        {isError && (
          <div className="rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
            Failed to load dashboard data. The backend may be unreachable.
          </div>
        )}

        {summary && (
          <>
            {/* Stat cards */}
            <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
              <StatCard
                title="Total Chargers"
                value={summary.totalChargers}
                change={`${summary.onlineChargers} online`}
                changePositive={summary.onlineChargers > 0}
              />
              <StatCard
                title="Active Sessions"
                value={summary.totalActiveTransactions}
                change={
                  summary.totalActiveTransactions > 0
                    ? `across ${new Set(summary.activeTransactions.map((t) => t.chargePointId)).size} charger${new Set(summary.activeTransactions.map((t) => t.chargePointId)).size === 1 ? '' : 's'}`
                    : 'No active sessions'
                }
                changePositive={summary.totalActiveTransactions > 0}
              />
              <StatCard
                title="Power Draw"
                value={`${summary.totalPowerDrawKw} kW`}
                change={`${summary.totalEnergyDeliveredKwh} kWh delivered`}
                changePositive={summary.totalPowerDrawKw > 0}
              />
              <StatCard
                title="System Faults"
                value={summary.connectorsByStatus.Faulted ?? 0}
                change={
                  (summary.connectorsByStatus.Faulted ?? 0) > 0
                    ? 'Action Required'
                    : 'All Clear'
                }
                changePositive={(summary.connectorsByStatus.Faulted ?? 0) === 0}
              />
            </div>

            {/* Quick-status banner */}
            <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
              {([
                { label: 'Online', value: summary.onlineChargers, icon: Activity, colour: 'text-emerald-400', bg: 'bg-emerald-400/10' },
                { label: 'Charging', value: summary.connectorsByStatus.Charging ?? 0, icon: Zap, colour: 'text-blue-400', bg: 'bg-blue-400/10' },
                { label: 'Available', value: summary.connectorsByStatus.Available ?? 0, icon: CheckCircle, colour: 'text-emerald-400', bg: 'bg-emerald-400/10' },
                { label: 'Faulted', value: summary.connectorsByStatus.Faulted ?? 0, icon: AlertTriangle, colour: 'text-red-400', bg: 'bg-red-400/10' },
              ] as const).map(({ label, value, icon: Icon, colour, bg }) => (
                <div key={label} className="flex items-center gap-3 rounded-lg bg-[#1a1f2e] border border-[#2a3042] p-4">
                  <div className={`flex h-10 w-10 items-center justify-center rounded-full ${bg}`}>
                    <Icon className={`h-5 w-5 ${colour}`} />
                  </div>
                  <div>
                    <div className={`text-xl font-bold ${colour}`}>{value}</div>
                    <div className="text-xs text-[#8892a4]">{label}</div>
                  </div>
                </div>
              ))}
            </div>

            {/* Active transactions */}
            <div className="rounded-lg bg-[#1a1f2e] border border-[#2a3042]">
              <div className="flex items-center justify-between border-b border-[#2a3042] px-4 py-3">
                <h2 className="text-sm font-semibold text-white">Active Transactions</h2>
                <Link to="/chargers" className="text-xs text-blue-400 hover:text-blue-300">
                  View all chargers →
                </Link>
              </div>

              {summary.activeTransactions.length === 0 ? (
                <div className="flex flex-col items-center justify-center py-10 text-[#8892a4]">
                  <BatteryCharging className="h-8 w-8 mb-2 opacity-40" />
                  <span className="text-sm">No active transactions</span>
                </div>
              ) : (
                <table className="w-full text-sm">
                  <thead>
                    <tr className="text-xs text-[#8892a4] border-b border-[#2a3042]">
                      <th className="px-4 py-2 text-left font-medium">Charger</th>
                      <th className="px-4 py-2 text-left font-medium">Connector</th>
                      <th className="px-4 py-2 text-left font-medium">ID Tag</th>
                      <th className="px-4 py-2 text-left font-medium">Status</th>
                      <th className="px-4 py-2 text-left font-medium">Duration</th>
                      <th className="px-4 py-2 text-right font-medium">Energy</th>
                      <th className="px-4 py-2 text-right font-medium">Power</th>
                    </tr>
                  </thead>
                  <tbody>
                    {summary.activeTransactions.map((tx) => (
                      <tr
                        key={`${tx.chargePointId}-${tx.transactionId}`}
                        className="border-b border-[#2a3042] last:border-0 hover:bg-[#232938] transition-colors"
                      >
                        <td className="px-4 py-3 font-mono text-xs text-white">{tx.chargePointId}</td>
                        <td className="px-4 py-3 text-[#8892a4]">#{tx.connectorId}</td>
                        <td className="px-4 py-3 font-mono text-xs text-[#8892a4]">{tx.idTag ?? '—'}</td>
                        <td className="px-4 py-3">
                          <ConnectorStatusBadge status={tx.connectorStatus as ConnectorStatus} />
                        </td>
                        <td className="px-4 py-3 text-[#8892a4]">{formatDuration(tx.startTime)}</td>
                        <td className="px-4 py-3 text-right text-white">{formatMeasurand(tx.energyActiveImportWh)}</td>
                        <td className="px-4 py-3 text-right text-white">{formatMeasurand(tx.powerActiveImport)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </>
        )}
      </div>
    </div>
  )
}

