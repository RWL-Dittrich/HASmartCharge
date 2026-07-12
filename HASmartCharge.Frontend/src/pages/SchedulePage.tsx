import { useEffect, useState } from 'react'
import { AlertTriangle, CalendarClock, Loader2, Trash2 } from 'lucide-react'
import { TopBar } from '@/components/layout/TopBar'
import { PriceChart } from '@/components/charts/PriceChart'
import { AutoScheduleCard } from '@/components/schedule/AutoScheduleCard'
import { OverridesCard } from '@/components/schedule/OverridesCard'
import { Badge } from '@/components/ui/Badge'
import { useCancelPlan, useCreatePlan, useCurrentPlan, usePlanPreview } from '@/hooks/usePlan'
import { usePrices } from '@/hooks/usePrices'
import { useCarSettings, usePriceSettings } from '@/hooks/useSettings'
import { formatDateTime, formatHoursDuration, formatKwh, formatMoney, localInputToUtcIso } from '@/lib/utils'
import { ApiError } from '@/api/client'

function defaultDeadlineLocal(): string {
  const tomorrow8am = new Date()
  tomorrow8am.setDate(tomorrow8am.getDate() + 1)
  tomorrow8am.setHours(8, 0, 0, 0)
  const offsetMs = tomorrow8am.getTimezoneOffset() * 60_000
  return new Date(tomorrow8am.getTime() - offsetMs).toISOString().slice(0, 16)
}

export function SchedulePage() {
  const { data: carSettings } = useCarSettings()
  const { data: priceSettings } = usePriceSettings()
  const { data: prices } = usePrices()
  const { data: activePlan } = useCurrentPlan()

  const [deadlineLocal, setDeadlineLocal] = useState(defaultDeadlineLocal)
  const [targetSoc, setTargetSoc] = useState<number | ''>('')
  const [debouncedDeadline, setDebouncedDeadline] = useState<string | null>(null)
  const [debouncedTarget, setDebouncedTarget] = useState<number | undefined>(undefined)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (carSettings && targetSoc === '') {
      setTargetSoc(carSettings.targetSocPercent)
    }
  }, [carSettings, targetSoc])

  useEffect(() => {
    const handle = setTimeout(() => {
      if (deadlineLocal) {
        setDebouncedDeadline(localInputToUtcIso(deadlineLocal))
        setDebouncedTarget(targetSoc === '' ? undefined : targetSoc)
      }
    }, 400)
    return () => clearTimeout(handle)
  }, [deadlineLocal, targetSoc])

  const { data: preview, isFetching: previewLoading } = usePlanPreview(debouncedDeadline, debouncedTarget)

  const createPlan = useCreatePlan()
  const cancelPlan = useCancelPlan()

  async function handleCreate() {
    if (!debouncedDeadline) return
    setError(null)
    if (!window.confirm('Create a new charge plan? Any existing active plan will be replaced.')) return
    try {
      await createPlan.mutateAsync({ deadlineUtc: debouncedDeadline, targetSocPercent: debouncedTarget })
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to create plan')
    }
  }

  async function handleCancel() {
    setError(null)
    if (!window.confirm('Cancel the active charge plan?')) return
    try {
      await cancelPlan.mutateAsync()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to cancel plan')
    }
  }

  return (
    <div className="flex flex-col h-full overflow-auto">
      <TopBar title="Schedule" subtitle="Plan a full charge before a deadline at the cheapest hours" />

      <div className="flex-1 p-6 space-y-6">
        {error && (
          <div className="rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
            {error}
          </div>
        )}

        {activePlan && (
          <div className="rounded-lg bg-[#1a1f2e] border border-[#2a3042] p-4 flex items-center justify-between">
            <div>
              <div className="flex items-center gap-2">
                <h2 className="text-sm font-semibold text-white">Active Plan</h2>
                <Badge tone={activePlan.status === 'Active' ? 'success' : 'info'}>{activePlan.status}</Badge>
              </div>
              <p className="text-xs text-[#8892a4] mt-1">
                Target {activePlan.targetSocPercent}% by {formatDateTime(activePlan.deadlineUtc)} · Est. cost{' '}
                {formatMoney(activePlan.estimatedCost, priceSettings?.currency)}
              </p>
            </div>
            <button
              onClick={handleCancel}
              disabled={cancelPlan.isPending}
              className="flex items-center gap-1.5 rounded-md border border-red-500/30 bg-red-500/10 px-3 py-2 text-sm font-medium text-red-400 transition-colors hover:bg-red-500/20 disabled:opacity-50"
            >
              {cancelPlan.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Trash2 className="h-4 w-4" />}
              Cancel plan
            </button>
          </div>
        )}

        <AutoScheduleCard />

        <OverridesCard />

        <div className="rounded-lg bg-[#1a1f2e] border border-[#2a3042] p-4 space-y-4">
          <h2 className="text-sm font-semibold text-white">Manual plan (one-off)</h2>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <label className="text-sm">
              <span className="text-[#8892a4] block mb-1">Deadline</span>
              <div className="relative">
                <CalendarClock className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[#8892a4]" />
                <input
                  type="datetime-local"
                  value={deadlineLocal}
                  onChange={(e) => setDeadlineLocal(e.target.value)}
                  className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] py-2 pl-9 pr-3 text-white outline-none focus:border-blue-500"
                />
              </div>
            </label>
            <label className="text-sm">
              <span className="text-[#8892a4] block mb-1">Target SoC (%)</span>
              <input
                type="number"
                min={1}
                max={100}
                value={targetSoc}
                onChange={(e) => setTargetSoc(e.target.value === '' ? '' : Number(e.target.value))}
                className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
              />
            </label>
          </div>

          {previewLoading && (
            <div className="flex items-center gap-2 text-sm text-[#8892a4]">
              <Loader2 className="h-4 w-4 animate-spin" /> Calculating…
            </div>
          )}

          {preview && !previewLoading && (
            <>
              {preview.socPercent == null && (
                <div className="flex items-center gap-2 rounded-md border border-amber-500/30 bg-amber-500/10 px-3 py-2 text-sm text-amber-400">
                  <AlertTriangle className="h-4 w-4 shrink-0" />
                  Current battery SoC is unavailable — configure the SoC entity in Settings for accurate planning.
                </div>
              )}
              {!preview.feasible && (
                <div className="flex items-center gap-2 rounded-md border border-red-500/30 bg-red-500/10 px-3 py-2 text-sm text-red-400">
                  <AlertTriangle className="h-4 w-4 shrink-0" />
                  {preview.warning ?? 'This deadline is not feasible with the current price data.'}
                </div>
              )}
              {preview.feasible && preview.warning && (
                <div className="flex items-center gap-2 rounded-md border border-amber-500/30 bg-amber-500/10 px-3 py-2 text-sm text-amber-400">
                  <AlertTriangle className="h-4 w-4 shrink-0" />
                  {preview.warning}
                </div>
              )}

              <div className="grid grid-cols-2 gap-3 text-sm sm:grid-cols-4">
                <div>
                  <div className="text-xs text-[#8892a4] uppercase tracking-wide">Status</div>
                  <div className="text-white font-medium">
                    {preview.done ? 'Already at target' : preview.feasible ? 'Feasible' : 'Not feasible'}
                  </div>
                </div>
                <div>
                  <div className="text-xs text-[#8892a4] uppercase tracking-wide">Energy Needed</div>
                  <div className="text-white font-medium">{formatKwh(preview.energyNeededKwh)}</div>
                </div>
                <div>
                  <div className="text-xs text-[#8892a4] uppercase tracking-wide">Charge Time</div>
                  <div className="text-white font-medium">{formatHoursDuration(preview.chargeDurationHours)}</div>
                </div>
                <div>
                  <div className="text-xs text-[#8892a4] uppercase tracking-wide">Estimated Cost</div>
                  <div className="text-white font-medium">
                    {formatMoney(preview.estimatedCost, priceSettings?.currency)}
                  </div>
                </div>
              </div>

              <PriceChart
                prices={prices ?? []}
                selectedHours={preview.selectedHours}
                currency={priceSettings?.currency}
              />
            </>
          )}

          <div className="flex justify-end pt-1">
            <button
              onClick={handleCreate}
              disabled={createPlan.isPending || !preview || !deadlineLocal}
              className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-500 disabled:opacity-50"
            >
              {createPlan.isPending ? 'Creating…' : 'Create plan'}
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
