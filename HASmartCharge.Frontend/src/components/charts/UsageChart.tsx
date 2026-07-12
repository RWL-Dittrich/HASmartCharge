import { useMemo, useState } from 'react'
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { currencySymbol, ensureUtcSuffix, formatKwh, formatMoney } from '@/lib/utils'
import type { ChargeSessionSummary } from '@/types/sessions'

type Granularity = 'day' | 'week' | 'month'
type Metric = 'cost' | 'energy'

interface UsageChartProps {
  sessions: ChargeSessionSummary[]
  currency?: string | null
  height?: number
}

interface Bucket {
  key: string
  sortTs: number
  label: string
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

/** Bucket start (local time) + label for a session start date at the chosen granularity. */
function bucketFor(date: Date, granularity: Granularity): { start: Date; label: string } {
  switch (granularity) {
    case 'day': {
      const start = new Date(date.getFullYear(), date.getMonth(), date.getDate())
      return { start, label: start.toLocaleDateString(undefined, { day: '2-digit', month: 'short' }) }
    }
    case 'week': {
      const start = startOfWeek(date)
      return {
        start,
        label: `Wk ${start.toLocaleDateString(undefined, { day: '2-digit', month: 'short' })}`,
      }
    }
    case 'month': {
      const start = new Date(date.getFullYear(), date.getMonth(), 1)
      return { start, label: start.toLocaleDateString(undefined, { month: 'short', year: 'numeric' }) }
    }
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
      <div className="font-medium text-white">{b.label}</div>
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

  const data = useMemo<Bucket[]>(() => {
    const buckets = new Map<string, Bucket>()
    for (const s of sessions) {
      const date = new Date(ensureUtcSuffix(s.startedAt))
      if (isNaN(date.getTime())) continue
      const { start, label } = bucketFor(date, granularity)
      const key = start.toISOString()
      let bucket = buckets.get(key)
      if (!bucket) {
        bucket = { key, sortTs: start.getTime(), label, cost: 0, energyKwh: 0, count: 0 }
        buckets.set(key, bucket)
      }
      bucket.cost += s.totalCost
      bucket.energyKwh += s.totalKwh
      bucket.count += 1
    }
    return [...buckets.values()].sort((a, b) => a.sortTs - b.sortTs)
  }, [sessions, granularity])

  const symbol = currencySymbol(currency)
  const dataKey = metric === 'cost' ? 'cost' : 'energyKwh'
  const barColor = metric === 'cost' ? BAR_COST : BAR_ENERGY

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

      {data.length === 0 ? (
        <div
          className="flex items-center justify-center rounded-lg border border-[#2a3042] bg-[#0f1117] text-sm text-[#8892a4]"
          style={{ height }}
        >
          No usage data yet
        </div>
      ) : (
        <div style={{ height }}>
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={data} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#2a3042" vertical={false} />
              <XAxis
                dataKey="label"
                tick={{ fill: '#8892a4', fontSize: 11 }}
                axisLine={{ stroke: '#2a3042' }}
                tickLine={false}
                interval="preserveStartEnd"
                minTickGap={16}
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
              <Tooltip
                content={<TooltipContent currency={currency} />}
                cursor={{ fill: '#232938' }}
              />
              <Bar dataKey={dataKey} fill={barColor} radius={[3, 3, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      )}
    </div>
  )
}
