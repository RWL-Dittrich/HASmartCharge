import { useMemo } from 'react'
import {
  Bar,
  BarChart,
  Cell,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { currencySymbol, ensureUtcSuffix, formatHourLabel } from '@/lib/utils'
import type { HourlyPrice } from '@/types/prices'

interface PriceChartProps {
  prices: HourlyPrice[]
  selectedHours?: string[]
  currency?: string | null
  height?: number
}

interface ChartPoint {
  hourStartUtc: string
  label: string
  price: number
  selected: boolean
  isCurrentHour: boolean
}

const BAR_DEFAULT = '#3b82f6'
const BAR_SELECTED = '#22c55e'
const BAR_PAST = '#3b4358'

function TooltipContent({ active, payload }: { active?: boolean; payload?: { payload: ChartPoint }[] }) {
  if (!active || !payload?.length) return null
  const point = payload[0].payload
  return (
    <div className="rounded-md border border-[#2a3042] bg-[#1a1f2e] px-3 py-2 text-xs shadow-lg">
      <div className="font-medium text-white">{point.label}</div>
      <div className="text-[#8892a4]">
        {point.price.toFixed(4)} / kWh
        {point.selected && <span className="ml-1 text-emerald-400">· selected</span>}
      </div>
    </div>
  )
}

export function PriceChart({ prices, selectedHours, currency, height = 220 }: PriceChartProps) {
  // Normalize to a canonical UTC ISO string: /api/prices and /api/plan/preview omit the "Z"
  // suffix (SQLite round-trips DateTime as Kind=Unspecified) while /api/plan includes it, so
  // raw string equality between the two would silently miss matches.
  const selectedSet = useMemo(
    () => new Set((selectedHours ?? []).map(ensureUtcSuffix)),
    [selectedHours],
  )
  const now = Date.now()

  const data: ChartPoint[] = useMemo(
    () =>
      prices.map((p) => {
        const hourStartIso = ensureUtcSuffix(p.hourStartUtc)
        const hourStart = new Date(hourStartIso).getTime()
        return {
          hourStartUtc: hourStartIso,
          label: formatHourLabel(p.hourStartUtc),
          price: p.pricePerKwh,
          selected: selectedSet.has(hourStartIso),
          isCurrentHour: now >= hourStart && now < hourStart + 60 * 60_000,
        }
      }),
    [prices, selectedSet, now],
  )

  const currentPoint = data.find((d) => d.isCurrentHour)
  const symbol = currencySymbol(currency)

  if (data.length === 0) {
    return (
      <div
        className="flex items-center justify-center rounded-lg border border-[#2a3042] bg-[#1a1f2e] text-sm text-[#8892a4]"
        style={{ height }}
      >
        No price data available
      </div>
    )
  }

  return (
    <div style={{ height }}>
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={data} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
          <XAxis
            dataKey="label"
            tick={{ fill: '#8892a4', fontSize: 11 }}
            axisLine={{ stroke: '#2a3042' }}
            tickLine={false}
            interval="preserveStartEnd"
            minTickGap={20}
          />
          <YAxis
            tick={{ fill: '#8892a4', fontSize: 11 }}
            axisLine={false}
            tickLine={false}
            width={40}
            tickFormatter={(v: number) => `${symbol}${v.toFixed(2)}`}
          />
          <Tooltip content={<TooltipContent />} cursor={{ fill: '#232938' }} />
          {currentPoint && (
            <ReferenceLine
              x={currentPoint.label}
              stroke="#f59e0b"
              strokeDasharray="4 4"
              label={{ value: 'now', position: 'insideTopRight', fill: '#f59e0b', fontSize: 11 }}
            />
          )}
          <Bar dataKey="price" radius={[3, 3, 0, 0]}>
            {data.map((point) => (
              <Cell
                key={point.hourStartUtc}
                fill={point.selected ? BAR_SELECTED : new Date(point.hourStartUtc).getTime() < now - 3600_000 ? BAR_PAST : BAR_DEFAULT}
              />
            ))}
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    </div>
  )
}
