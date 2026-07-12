import { useState } from 'react'
import { CalendarPlus, Loader2, Trash2 } from 'lucide-react'
import { useAutoSchedule, useDeleteOverride, useUpsertOverride } from '@/hooks/useAutoSchedule'
import { useCarSettings } from '@/hooks/useSettings'
import { ApiError } from '@/api/client'

/** Today's local date as yyyy-MM-dd, used to hide past overrides and floor the date picker. */
function todayLocalIso(): string {
  const now = new Date()
  const offsetMs = now.getTimezoneOffset() * 60_000
  return new Date(now.getTime() - offsetMs).toISOString().slice(0, 10)
}

/** Formats a yyyy-MM-dd date string as a readable local date, e.g. "Fri 17 Jul". */
function formatDate(iso: string): string {
  const date = new Date(`${iso}T00:00:00`)
  if (isNaN(date.getTime())) return iso
  return date.toLocaleDateString(undefined, { weekday: 'short', day: '2-digit', month: 'short' })
}

export function OverridesCard() {
  const { data, isLoading } = useAutoSchedule()
  const { data: carSettings } = useCarSettings()
  const upsert = useUpsertOverride()
  const remove = useDeleteOverride()

  const [dateLocal, setDateLocal] = useState('')
  const [departureLocal, setDepartureLocal] = useState('08:00')
  const [targetSoc, setTargetSoc] = useState<number | ''>('')
  const [error, setError] = useState<string | null>(null)

  const carDefaultSoc = carSettings?.targetSocPercent ?? 100
  const today = todayLocalIso()
  const upcoming = (data?.overrides ?? []).filter((o) => o.dateLocal >= today)

  async function handleAdd() {
    setError(null)
    if (!dateLocal || !departureLocal) return
    try {
      await upsert.mutateAsync({
        dateLocal,
        departureLocal,
        targetSocPercent: targetSoc === '' ? null : targetSoc,
      })
      setDateLocal('')
      setDepartureLocal('08:00')
      setTargetSoc('')
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to add override')
    }
  }

  async function handleDelete(id: number) {
    setError(null)
    try {
      await remove.mutateAsync(id)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to delete override')
    }
  }

  return (
    <div className="rounded-lg bg-[#1a1f2e] border border-[#2a3042] p-4 space-y-4">
      <h2 className="text-sm font-semibold text-white">Overrides</h2>
      <p className="text-xs text-[#8892a4]">
        Set a different departure time for a specific date (e.g. a day off → leave later so the car charges
        during the cheaper daytime hours). An override replaces the weekly time for that date only.
      </p>

      <div className="flex flex-wrap items-end gap-3">
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Date</span>
          <input
            type="date"
            min={today}
            value={dateLocal}
            onChange={(e) => setDateLocal(e.target.value)}
            className="rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-1.5 text-sm text-white outline-none focus:border-blue-500"
          />
        </label>
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Departure</span>
          <input
            type="time"
            value={departureLocal}
            onChange={(e) => setDepartureLocal(e.target.value)}
            className="rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-1.5 text-sm text-white outline-none focus:border-blue-500"
          />
        </label>
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Target % SoC</span>
          <input
            type="number"
            min={1}
            max={100}
            placeholder={String(carDefaultSoc)}
            value={targetSoc}
            onChange={(e) => setTargetSoc(e.target.value === '' ? '' : Number(e.target.value))}
            className="w-24 rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-1.5 text-sm text-white outline-none focus:border-blue-500"
          />
        </label>
        <button
          onClick={handleAdd}
          disabled={upsert.isPending || !dateLocal}
          className="flex items-center gap-1.5 rounded-md bg-blue-600 px-3 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-500 disabled:opacity-50"
        >
          {upsert.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <CalendarPlus className="h-4 w-4" />}
          Add override
        </button>
      </div>

      {error && <div className="text-sm text-red-400">{error}</div>}

      {isLoading ? (
        <div className="flex items-center gap-2 text-sm text-[#8892a4]">
          <Loader2 className="h-4 w-4 animate-spin" /> Loading…
        </div>
      ) : upcoming.length === 0 ? (
        <p className="text-sm text-[#8892a4]">No upcoming overrides.</p>
      ) : (
        <ul className="divide-y divide-[#2a3042] rounded-md border border-[#2a3042]">
          {upcoming.map((o) => (
            <li key={o.id} className="flex items-center justify-between px-3 py-2 text-sm">
              <span className="text-[#c3cad8]">
                <span className="font-medium text-white">{formatDate(o.dateLocal)}</span> — leave {o.departureLocal}
                {' '}→ {o.targetSocPercent ?? carDefaultSoc}%
              </span>
              <button
                onClick={() => handleDelete(o.id)}
                disabled={remove.isPending}
                className="flex items-center gap-1 rounded-md border border-red-500/30 bg-red-500/10 px-2 py-1 text-xs font-medium text-red-400 transition-colors hover:bg-red-500/20 disabled:opacity-50"
              >
                <Trash2 className="h-3.5 w-3.5" />
                Remove
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
