import { useMemo, useState } from 'react'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { currencySymbol, ensureUtcSuffix, formatKwh, formatMoney } from '@/lib/utils'
import type { ChargeSessionSummary } from '@/types/sessions'

type Granularity = 'day' | 'week' | 'month'
type Metric = 'cost' | 'energy'

/**
 * Each granularity is shown across one window of the next unit up, so the bar count stays readable
 * and the axis is always a complete calendar period — every day, week or month is drawn, including
 * the ones with no sessions.
 */
const WINDOW: Record<Granularity, 'week' | 'month' | 'year'> = {
  day: 'week',
  week: 'month',
  month: 'year',
}

interface UsageChartProps {
  sessions: ChargeSessionSummary[]
  currency?: string | null
  height?: number
}

interface Bucket {
  key: number
  /** Short axis label; the window heading already carries the month/year context. */
  label: string
  /** Unabbreviated label for the tooltip. */
  fullLabel: string
  cost: number
  energyKwh: number
  count: number
}

const BAR_COST = '#3b82f6'
const BAR_ENERGY = '#22c55e'

/** Local Monday-start week for a date. */
function startOfWeek(d: Date): Date {
  const x = new Date(d.getFullYear(), d.getMonth(), d.getDate())
  const offset = (x.getDay() + 6) % 7 // 0 = Monday
  x.setDate(x.getDate() - offset)
  return x
}

/** Start (local midnight) of the bucket a date falls in. */
function bucketStartFor(date: Date, granularity: Granularity): Date {
  switch (granularity) {
    case 'day':
      return new Date(date.getFullYear(), date.getMonth(), date.getDate())
    case 'week':
      return startOfWeek(date)
    case 'month':
      return new Date(date.getFullYear(), date.getMonth(), 1)
  }
}

/** Start of the window (week/month/year) that contains a date. */
function startOfWindow(date: Date, granularity: Granularity): Date {
  switch (WINDOW[granularity]) {
    case 'week':
      return startOfWeek(date)
    case 'month':
      return new Date(date.getFullYear(), date.getMonth(), 1)
    case 'year':
      return new Date(date.getFullYear(), 0, 1)
  }
}

/** The window `delta` steps away (±1 week / month / year). */
function shiftWindow(windowStart: Date, granularity: Granularity, delta: number): Date {
  switch (WINDOW[granularity]) {
    case 'week':
      return new Date(windowStart.getFullYear(), windowStart.getMonth(), windowStart.getDate() + delta * 7)
    case 'month':
      return new Date(windowStart.getFullYear(), windowStart.getMonth() + delta, 1)
    case 'year':
      return new Date(windowStart.getFullYear() + delta, 0, 1)
  }
}

/**
 * Every bucket start in the window, session data or not. A week straddling two months shows up in
 * both months' windows — it belongs to both, and each window stays a complete calendar period.
 */
function bucketStartsIn(windowStart: Date, granularity: Granularity): Date[] {
  const year = windowStart.getFullYear()
  const month = windowStart.getMonth()
  const starts: Date[] = []

  switch (granularity) {
    case 'day':
      for (let i = 0; i < 7; i++) {
        starts.push(new Date(year, month, windowStart.getDate() + i))
      }
      break
    case 'week': {
      const nextMonth = new Date(year, month + 1, 1).getTime()
      for (let cur = startOfWeek(windowStart); cur.getTime() < nextMonth; ) {
        starts.push(cur)
        cur = new Date(cur.getFullYear(), cur.getMonth(), cur.getDate() + 7)
      }
      break
    }
    case 'month':
      for (let i = 0; i < 12; i++) {
        starts.push(new Date(year, i, 1))
      }
      break
  }

  return starts
}

function bucketLabels(start: Date, granularity: Granularity): { label: string; fullLabel: string } {
  switch (granularity) {
    case 'day':
      return {
        label: start.toLocaleDateString(undefined, { weekday: 'short', day: '2-digit' }),
        fullLabel: start.toLocaleDateString(undefined, { weekday: 'long', day: '2-digit', month: 'short' }),
      }
    case 'week': {
      const end = new Date(start.getFullYear(), start.getMonth(), start.getDate() + 6)
      const short = { day: '2-digit', month: 'short' } as const
      return {
        label: start.toLocaleDateString(undefined, short),
        fullLabel: `Week of ${start.toLocaleDateString(undefined, short)} – ${end.toLocaleDateString(undefined, short)}`,
      }
    }
    case 'month':
      return {
        label: start.toLocaleDateString(undefined, { month: 'short' }),
        fullLabel: start.toLocaleDateString(undefined, { month: 'long', year: 'numeric' }),
      }
  }
}

function windowLabel(windowStart: Date, granularity: Granularity): string {
  switch (WINDOW[granularity]) {
    case 'week': {
      const end = new Date(windowStart.getFullYear(), windowStart.getMonth(), windowStart.getDate() + 6)
      const short = { day: '2-digit', month: 'short' } as const
      return `${windowStart.toLocaleDateString(undefined, short)} – ${end.toLocaleDateString(undefined, { ...short, year: 'numeric' })}`
    }
    case 'month':
      return windowStart.toLocaleDateString(undefined, { month: 'long', year: 'numeric' })
    case 'year':
      return String(windowStart.getFullYear())
  }
}

function SegmentedControl<T extends string>({
  value,
  options,
  onChange,
}: {
  value: T
  options: { value: T; label: string }[]
  onChange: (v: T) => void
}) {
  return (
    <div className="inline-flex rounded-md border border-[#2a3042] bg-[#0f1117] p-0.5">
      {options.map((o) => (
        <button
          key={o.value}
          type="button"
          onClick={() => onChange(o.value)}
          className={`rounded px-2.5 py-1 text-xs font-medium transition-colors ${
            value === o.value ? 'bg-[#2a3042] text-white' : 'text-[#8892a4] hover:text-white'
          }`}
        >
          {o.label}
        </button>
      ))}
    </div>
  )
}

function TooltipContent({
  active,
  payload,
  currency,
}: {
  active?: boolean
  payload?: { payload: Bucket }[]
  currency?: string | null
}) {
  if (!active || !payload?.length) return null
  const b = payload[0].payload
  return (
    <div className="rounded-md border border-[#2a3042] bg-[#1a1f2e] px-3 py-2 text-xs shadow-lg space-y-0.5">
      <div className="font-medium text-white">{b.fullLabel}</div>
      <div className="text-[#8892a4]">{formatMoney(b.cost, currency)}</div>
      <div className="text-[#8892a4]">{formatKwh(b.energyKwh)}</div>
      <div className="text-[#8892a4]">
        {b.count} session{b.count === 1 ? '' : 's'}
      </div>
    </div>
  )
}

export function UsageChart({ sessions, currency, height = 260 }: UsageChartProps) {
  const [granularity, setGranularity] = useState<Granularity>('day')
  const [metric, setMetric] = useState<Metric>('cost')
  // A date inside the visible window. Kept as a date rather than a window start so switching
  // granularity keeps you around the same point in time instead of jumping back to today.
  const [anchor, setAnchor] = useState<Date>(() => new Date())

  const windowStart = useMemo(() => startOfWindow(anchor, granularity), [anchor, granularity])
  const currentWindowStart = useMemo(() => startOfWindow(new Date(), granularity), [granularity])
  const isCurrentWindow = windowStart.getTime() === currentWindowStart.getTime()
  // Sessions are only ever recorded in the past, so a future window is always empty.
  const canGoNext = shiftWindow(windowStart, granularity, 1).getTime() <= currentWindowStart.getTime()

  const data = useMemo<Bucket[]>(() => {
    const buckets = new Map<number, Bucket>()
    for (const start of bucketStartsIn(windowStart, granularity)) {
      buckets.set(start.getTime(), {
        key: start.getTime(),
        ...bucketLabels(start, granularity),
        cost: 0,
        energyKwh: 0,
        count: 0,
      })
    }

    for (const s of sessions) {
      const date = new Date(ensureUtcSuffix(s.startedAt))
      if (isNaN(date.getTime())) continue
      const bucket = buckets.get(bucketStartFor(date, granularity).getTime())
      if (!bucket) continue // outside the visible window
      bucket.cost += s.totalCost
      bucket.energyKwh += s.totalKwh
      bucket.count += 1
    }

    return [...buckets.values()].sort((a, b) => a.key - b.key)
  }, [sessions, granularity, windowStart])

  const symbol = currencySymbol(currency)
  const dataKey = metric === 'cost' ? 'cost' : 'energyKwh'
  const barColor = metric === 'cost' ? BAR_COST : BAR_ENERGY
  const navButton =
    'rounded-md border border-[#2a3042] p-1 text-[#8892a4] transition-colors hover:text-white hover:bg-[#232938] disabled:opacity-30 disabled:hover:bg-transparent disabled:hover:text-[#8892a4]'

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <SegmentedControl
          value={metric}
          onChange={setMetric}
          options={[
            { value: 'cost', label: 'Cost' },
            { value: 'energy', label: 'Energy' },
          ]}
        />
        <SegmentedControl
          value={granularity}
          onChange={setGranularity}
          options={[
            { value: 'day', label: 'Day' },
            { value: 'week', label: 'Week' },
            { value: 'month', label: 'Month' },
          ]}
        />
      </div>

      <div className="flex items-center justify-center gap-3">
        <button
          type="button"
          onClick={() => setAnchor(shiftWindow(windowStart, granularity, -1))}
          title={`Previous ${WINDOW[granularity]}`}
          className={navButton}
        >
          <ChevronLeft className="h-4 w-4" />
        </button>
        <span className="min-w-[10rem] text-center text-sm font-medium text-white">
          {windowLabel(windowStart, granularity)}
        </span>
        <button
          type="button"
          onClick={() => setAnchor(shiftWindow(windowStart, granularity, 1))}
          disabled={!canGoNext}
          title={`Next ${WINDOW[granularity]}`}
          className={navButton}
        >
          <ChevronRight className="h-4 w-4" />
        </button>
        {!isCurrentWindow && (
          <button
            type="button"
            onClick={() => setAnchor(new Date())}
            className="rounded-md border border-[#2a3042] px-2 py-1 text-xs text-[#8892a4] transition-colors hover:bg-[#232938] hover:text-white"
          >
            Today
          </button>
        )}
      </div>

      <div style={{ height }}>
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={data} margin={{ top: 8, right: 8, left: 0, bottom: 0 }} accessibilityLayer={false}>
            <CartesianGrid strokeDasharray="3 3" stroke="#2a3042" vertical={false} />
            <XAxis
              dataKey="label"
              tick={{ fill: '#8892a4', fontSize: 11 }}
              axisLine={{ stroke: '#2a3042' }}
              tickLine={false}
              // A window holds at most 12 bars, so every period gets its own label.
              interval={0}
            />
            <YAxis
              tick={{ fill: '#8892a4', fontSize: 11 }}
              axisLine={false}
              tickLine={false}
              width={48}
              tickFormatter={(v: number) =>
                metric === 'cost' ? `${symbol}${v.toFixed(0)}` : `${v.toFixed(0)}`
              }
            />
            <Tooltip content={<TooltipContent currency={currency} />} cursor={{ fill: '#232938' }} />
            <Bar dataKey={dataKey} fill={barColor} radius={[3, 3, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </div>
  )
}
