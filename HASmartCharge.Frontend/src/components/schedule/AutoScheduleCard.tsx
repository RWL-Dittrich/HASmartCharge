import { useEffect, useState } from 'react'
import { CalendarClock, Loader2 } from 'lucide-react'
import { useAutoSchedule, useUpdateAutoSchedule } from '@/hooks/useAutoSchedule'
import { useCarSettings } from '@/hooks/useSettings'
import type { AutoScheduleSettings, WeeklyDeparture } from '@/types/autoSchedule'
import { formatDateTime } from '@/lib/utils'
import { ApiError } from '@/api/client'

// Monday-first display order over System.DayOfWeek values (0 = Sunday).
const DAY_ORDER = [1, 2, 3, 4, 5, 6, 0]
const DAY_LABELS: Record<number, string> = {
  0: 'Sunday',
  1: 'Monday',
  2: 'Tuesday',
  3: 'Wednesday',
  4: 'Thursday',
  5: 'Friday',
  6: 'Saturday',
}

type Draft = Pick<AutoScheduleSettings, 'enabled' | 'timeZoneId' | 'weekly'>

export function AutoScheduleCard() {
  const { data, isLoading } = useAutoSchedule()
  const { data: carSettings } = useCarSettings()
  const update = useUpdateAutoSchedule()

  const carDefaultSoc = carSettings?.targetSocPercent ?? 100

  const [form, setForm] = useState<Draft | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [savedAt, setSavedAt] = useState<number | null>(null)

  useEffect(() => {
    if (data && !form) {
      setForm({ enabled: data.enabled, timeZoneId: data.timeZoneId, weekly: data.weekly })
    }
  }, [data, form])

  if (isLoading || !form) {
    return (
      <div className="rounded-lg bg-[#1a1f2e] border border-[#2a3042] p-4">
        <div className="flex items-center gap-2 text-sm text-[#8892a4]">
          <Loader2 className="h-4 w-4 animate-spin" /> Loading…
        </div>
      </div>
    )
  }

  const draft = form

  function setDay(dayOfWeek: number, patch: Partial<WeeklyDeparture>) {
    setForm({
      ...draft,
      weekly: draft.weekly.map((w) => (w.dayOfWeek === dayOfWeek ? { ...w, ...patch } : w)),
    })
  }

  async function handleSave() {
    setError(null)
    try {
      await update.mutateAsync(draft)
      setSavedAt(Date.now())
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to save auto-schedule')
    }
  }

  return (
    <div className="rounded-lg bg-[#1a1f2e] border border-[#2a3042] p-4 space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold text-white">Automatic schedule</h2>
        <label className="flex items-center gap-2 text-sm text-[#c3cad8]">
          <input
            type="checkbox"
            checked={draft.enabled}
            onChange={(e) => setForm({ ...draft, enabled: e.target.checked })}
            className="h-4 w-4 accent-blue-600"
          />
          Enabled
        </label>
      </div>

      <p className="text-xs text-[#8892a4]">
        When enabled, plugging in the car creates a charge plan to be full by the next departure time below,
        charging at the cheapest hours. Times are local ({draft.timeZoneId}).
      </p>

      <div className="space-y-2">
        {DAY_ORDER.map((day) => {
          const entry = draft.weekly.find((w) => w.dayOfWeek === day)
          if (!entry) return null
          return (
            <div key={day} className="flex flex-wrap items-center gap-2 sm:gap-3">
              <label className="flex items-center gap-2 w-28 shrink-0 text-sm text-[#c3cad8] sm:w-32">
                <input
                  type="checkbox"
                  checked={entry.enabled}
                  onChange={(e) => setDay(day, { enabled: e.target.checked })}
                  className="h-4 w-4 accent-blue-600"
                />
                {DAY_LABELS[day]}
              </label>
              <input
                type="time"
                value={entry.departureLocal}
                disabled={!entry.enabled}
                onChange={(e) => setDay(day, { departureLocal: e.target.value })}
                className="rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-1.5 text-sm text-white outline-none focus:border-blue-500 disabled:opacity-40"
              />
              <div className="flex items-center gap-1.5">
                <input
                  type="number"
                  min={1}
                  max={100}
                  placeholder={String(carDefaultSoc)}
                  value={entry.targetSocPercent ?? ''}
                  disabled={!entry.enabled}
                  onChange={(e) =>
                    setDay(day, { targetSocPercent: e.target.value === '' ? null : Number(e.target.value) })
                  }
                  className="w-20 rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-1.5 text-sm text-white outline-none focus:border-blue-500 disabled:opacity-40"
                />
                <span className="text-sm text-[#8892a4]">% SoC</span>
              </div>
            </div>
          )
        })}
      </div>

      <div className="flex items-center gap-2 rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-sm text-[#c3cad8]">
        <CalendarClock className="h-4 w-4 text-[#8892a4]" />
        Next departure:{' '}
        <span className="font-medium text-white">
          {data?.nextDepartureUtc ? formatDateTime(data.nextDepartureUtc) : 'none configured'}
        </span>
        {data?.nextDepartureUtc && (
          <span className="text-[#8892a4]">→ {data.nextTargetSocPercent ?? carDefaultSoc}%</span>
        )}
      </div>

      {error && <div className="text-sm text-red-400">{error}</div>}

      <div className="flex items-center justify-end gap-3 pt-1">
        {savedAt && !update.isPending && <span className="text-xs text-green-400">Saved</span>}
        <button
          onClick={handleSave}
          disabled={update.isPending}
          className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-500 disabled:opacity-50"
        >
          {update.isPending ? 'Saving…' : 'Save schedule'}
        </button>
      </div>
    </div>
  )
}
