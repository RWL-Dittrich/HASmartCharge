import { useMemo, useState } from 'react'
import { BatteryCharging, ChevronDown, ChevronRight, Loader2, Trash2 } from 'lucide-react'
import { TopBar } from '@/components/layout/TopBar'
import { UsageChart } from '@/components/charts/UsageChart'
import { useDeleteSession, useSessionDetail, useSessions } from '@/hooks/useSessions'
import { usePriceSettings } from '@/hooks/useSettings'
import { ensureUtcSuffix, formatDateTime, formatDuration, formatKwh, formatMoney, formatPricePerKwh } from '@/lib/utils'

function SessionRow({ transactionId, currency }: { transactionId: number; currency?: string | null }) {
  const [expanded, setExpanded] = useState(false)
  const { data: sessions } = useSessions()
  const session = sessions?.find((s) => s.transactionId === transactionId)
  const { data: detail, isLoading } = useSessionDetail(expanded ? transactionId : null)
  const deleteSession = useDeleteSession()

  if (!session) return null

  const handleDelete = (e: React.MouseEvent) => {
    e.stopPropagation()
    if (!window.confirm('Delete this session? This cannot be undone.')) return
    deleteSession.mutate(transactionId)
  }

  return (
    <>
      <tr
        className="border-b border-[#2a3042] last:border-0 hover:bg-[#232938] transition-colors cursor-pointer"
        onClick={() => setExpanded((v) => !v)}
      >
        <td className="px-4 py-3">
          {expanded ? (
            <ChevronDown className="h-4 w-4 text-[#8892a4]" />
          ) : (
            <ChevronRight className="h-4 w-4 text-[#8892a4]" />
          )}
        </td>
        <td className="px-4 py-3 text-white">{formatDateTime(session.startedAt)}</td>
        <td className="px-4 py-3 text-[#8892a4]">{formatDuration(session.startedAt, session.completedAt)}</td>
        <td className="px-4 py-3 text-right text-white">{formatKwh(session.totalKwh)}</td>
        <td className="px-4 py-3 text-right text-white">{formatMoney(session.totalCost, currency)}</td>
        <td className="px-4 py-3 text-right text-[#8892a4]">{formatPricePerKwh(session.avgPricePerKwh, currency)}</td>
        <td className="px-4 py-3 text-right">
          <button
            type="button"
            onClick={handleDelete}
            disabled={deleteSession.isPending}
            title="Delete session"
            className="p-1 rounded text-[#8892a4] hover:text-red-400 hover:bg-red-500/10 transition-colors disabled:opacity-40"
          >
            {deleteSession.isPending ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <Trash2 className="h-4 w-4" />
            )}
          </button>
        </td>
      </tr>
      {expanded && (
        <tr className="border-b border-[#2a3042] last:border-0 bg-[#0f1117]/40">
          <td colSpan={7} className="px-4 py-3">
            {isLoading ? (
              <div className="flex items-center gap-2 text-sm text-[#8892a4] py-4">
                <Loader2 className="h-4 w-4 animate-spin" /> Loading breakdown…
              </div>
            ) : !detail || detail.hourlyBreakdown.length === 0 ? (
              <div className="text-sm text-[#8892a4] py-4">No hourly breakdown available.</div>
            ) : (
              <table className="w-full text-xs">
                <thead>
                  <tr className="text-[#8892a4]">
                    <th className="px-2 py-1 text-left font-medium">Hour</th>
                    <th className="px-2 py-1 text-right font-medium">Energy</th>
                    <th className="px-2 py-1 text-right font-medium">Price</th>
                    <th className="px-2 py-1 text-right font-medium">Cost</th>
                  </tr>
                </thead>
                <tbody>
                  {detail.hourlyBreakdown.map((h) => (
                    <tr key={h.hourStartUtc}>
                      <td className="px-2 py-1 text-white">{formatDateTime(h.hourStartUtc)}</td>
                      <td className="px-2 py-1 text-right text-white">{formatKwh(h.energyKwh)}</td>
                      <td className="px-2 py-1 text-right text-[#8892a4]">
                        {formatPricePerKwh(h.pricePerKwh, currency)}
                      </td>
                      <td className="px-2 py-1 text-right text-white">{formatMoney(h.cost, currency)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </td>
        </tr>
      )}
    </>
  )
}

export function HistoryPage() {
  const { data: sessions, isLoading, isError } = useSessions()
  const { data: priceSettings } = usePriceSettings()

  // Newest-first (the backend already orders desc, but pin it so the table order never depends on that).
  const sortedSessions = useMemo(
    () =>
      sessions
        ? [...sessions].sort(
            (a, b) =>
              new Date(ensureUtcSuffix(b.startedAt)).getTime() -
              new Date(ensureUtcSuffix(a.startedAt)).getTime(),
          )
        : undefined,
    [sessions],
  )

  return (
    <div className="flex flex-col h-full overflow-auto">
      <TopBar title="History" subtitle="Past charging sessions and their cost breakdown" />

      <div className="flex-1 p-4 space-y-4 sm:p-6">
        {sortedSessions && sortedSessions.length > 0 && (
          <div className="rounded-lg bg-[#1a1f2e] border border-[#2a3042] p-4">
            <h2 className="mb-3 text-sm font-semibold text-white">Usage over time</h2>
            <UsageChart sessions={sortedSessions} currency={priceSettings?.currency} />
          </div>
        )}

        {isLoading && (
          <div className="flex items-center justify-center py-20">
            <Loader2 className="h-6 w-6 text-blue-400 animate-spin" />
            <span className="ml-2 text-sm text-[#8892a4]">Loading sessions…</span>
          </div>
        )}

        {isError && (
          <div className="rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
            Failed to load session history. The backend may be unreachable.
          </div>
        )}

        {sortedSessions && (
          <div className="rounded-lg bg-[#1a1f2e] border border-[#2a3042]">
            {sortedSessions.length === 0 ? (
              <div className="flex flex-col items-center justify-center py-16 text-[#8892a4]">
                <BatteryCharging className="h-8 w-8 mb-2 opacity-40" />
                <span className="text-sm">No charge sessions yet</span>
              </div>
            ) : (
              <div className="overflow-x-auto">
              <table className="w-full min-w-[560px] text-sm">
                <thead>
                  <tr className="text-xs text-[#8892a4] border-b border-[#2a3042]">
                    <th className="px-4 py-2 w-8" />
                    <th className="px-4 py-2 text-left font-medium">Start</th>
                    <th className="px-4 py-2 text-left font-medium">Duration</th>
                    <th className="px-4 py-2 text-right font-medium">Energy</th>
                    <th className="px-4 py-2 text-right font-medium">Cost</th>
                    <th className="px-4 py-2 text-right font-medium">Avg Price</th>
                    <th className="px-4 py-2 w-8" />
                  </tr>
                </thead>
                <tbody>
                  {sortedSessions.map((s) => (
                    <SessionRow
                      key={s.transactionId}
                      transactionId={s.transactionId}
                      currency={priceSettings?.currency}
                    />
                  ))}
                </tbody>
              </table>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  )
}
