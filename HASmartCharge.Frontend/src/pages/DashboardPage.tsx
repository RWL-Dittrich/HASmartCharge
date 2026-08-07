import { useEffect, useMemo, useState } from 'react'
import { AlertTriangle, BatteryCharging, CheckCircle, Loader2, Play, Square, Wifi, WifiOff, XCircle } from 'lucide-react'
import { TopBar } from '@/components/layout/TopBar'
import { StatCard } from '@/components/ui/StatCard'
import { Badge } from '@/components/ui/Badge'
import { PriceChart } from '@/components/charts/PriceChart'
import { usePrices } from '@/hooks/usePrices'
import { useCurrentPlan } from '@/hooks/usePlan'
import { useChargerStatus, useSetChargerAvailability, useSetChargerPower } from '@/hooks/useCharger'
import { useHaStatus } from '@/hooks/useHa'
import { useChargerSettings, usePriceSettings } from '@/hooks/useSettings'
import { useStartCharge, useStopCharge } from '@/hooks/useCharge'
import { usePlanPreview } from '@/hooks/usePlan'
import {
  ensureUtcSuffix,
  formatDateTime,
  formatDuration,
  formatHourLabel,
  formatKw,
  formatKwh,
  formatMoney,
  formatPricePerKwh,
} from '@/lib/utils'
import { ApiError } from '@/api/client'
import type { ChargePlanStatus } from '@/types/plan'

const PLAN_STATUS_TONE: Record<ChargePlanStatus, 'neutral' | 'success' | 'warning' | 'danger' | 'info'> = {
  Pending: 'info',
  Active: 'success',
  Completed: 'neutral',
  Cancelled: 'neutral',
  MissedDeadline: 'danger',
}

export function DashboardPage() {
  const [actionError, setActionError] = useState<string | null>(null)

  const socDeadline = useMemo(() => new Date(Date.now() + 24 * 3_600_000).toISOString(), [])
  const { data: socPreview } = usePlanPreview(socDeadline)

  const { data: prices, isLoading: pricesLoading } = usePrices()
  const { data: plan } = useCurrentPlan()
  const { data: chargerStatus, isLoading: chargerLoading } = useChargerStatus()
  const { data: haStatus } = useHaStatus()
  const { data: priceSettings } = usePriceSettings()
  const { data: chargerSettings } = useChargerSettings()

  const startCharge = useStartCharge()
  const stopCharge = useStopCharge()
  const setAvailability = useSetChargerAvailability()
  const setPower = useSetChargerPower()

  // Local slider value, seeded once from the persisted setpoint.
  const [powerKw, setPowerKw] = useState<number | null>(null)
  useEffect(() => {
    if (chargerSettings && powerKw === null) setPowerKw(chargerSettings.chargePowerSetpointKw)
  }, [chargerSettings, powerKw])

  // Average €/kWh the plan is expected to pay — cost already includes the (cheapest-first)
  // hour selection, so this is just cost ÷ energy rather than a mean of the hourly prices.
  const avgPricePerKwh =
    plan && plan.estimatedEnergyKwh > 0 ? plan.estimatedCost / plan.estimatedEnergyKwh : null

  // Recomputed each render (cheap) so the "current hour" chip follows the clock as queries refetch.
  const currentHourStartMs = new Date().setUTCMinutes(0, 0, 0)

  const priceByHour = useMemo(
    () => new Map((prices ?? []).map((p) => [ensureUtcSuffix(p.hourStartUtc), p.pricePerKwh])),
    [prices],
  )

  const deadlineIn = plan ? formatDuration(new Date().toISOString(), plan.deadlineUtc) : null

  async function handleSetPower(kw: number) {
    setActionError(null)
    try {
      const result = await setPower.mutateAsync(kw)
      setPowerKw(result.setpointKw) // reflect the server-clamped value
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : 'Failed to set charge power')
    }
  }

  async function handleStart() {
    setActionError(null)
    try {
      await startCharge.mutateAsync(undefined)
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : 'Failed to start charging')
    }
  }

  async function handleStop() {
    setActionError(null)
    try {
      await stopCharge.mutateAsync(undefined)
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : 'Failed to stop charging')
    }
  }

  async function handleSetAvailability(available: boolean) {
    setActionError(null)
    try {
      await setAvailability.mutateAsync(available)
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : 'Failed to change availability')
    }
  }

  return (
    <div className="flex flex-col h-full overflow-auto">
      <TopBar title="Dashboard" subtitle="Live status of your car, charger, and charge plan" />

      <div className="flex-1 p-4 space-y-4 sm:p-6 sm:space-y-6">
        {actionError && (
          <div className="rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
            {actionError}
          </div>
        )}

        <div className="grid grid-cols-2 gap-3 sm:gap-4 lg:grid-cols-4">
          <StatCard
            title="Battery SoC"
            value={socPreview?.socPercent != null ? `${Math.round(socPreview.socPercent)}%` : '—'}
            change={socPreview?.warning ?? undefined}
            changePositive={!socPreview?.warning}
          />
          <StatCard
            title="Charger"
            value={chargerStatus?.connectorStatus ?? '—'}
            change={chargerStatus?.connected ? 'Online' : 'Offline'}
            changePositive={chargerStatus?.connected}
          />
          <StatCard
            title="Live Power"
            value={formatKw(chargerStatus?.currentPowerKw)}
            change={`${formatKwh(chargerStatus?.sessionEnergyKwh)} this session`}
            changePositive={(chargerStatus?.currentPowerKw ?? 0) > 0}
          />
          <StatCard
            title="Home Assistant"
            value={haStatus?.connected ? 'Connected' : 'Disconnected'}
            change={haStatus?.baseUrl ?? undefined}
            changePositive={haStatus?.connected}
          />
        </div>

        <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
          {/* Charger card */}
          <div className="rounded-lg bg-[#1a1f2e] border border-[#2a3042] p-4 space-y-3">
            <div className="flex items-center justify-between">
              <h2 className="text-sm font-semibold text-white">Charger</h2>
              {chargerStatus?.connected ? (
                <Badge tone="success" pulse>
                  <Wifi className="h-3 w-3" /> Online
                </Badge>
              ) : (
                <Badge tone="danger">
                  <WifiOff className="h-3 w-3" /> Offline
                </Badge>
              )}
            </div>
            {chargerLoading ? (
              <div className="flex items-center gap-2 text-sm text-[#8892a4]">
                <Loader2 className="h-4 w-4 animate-spin" /> Loading…
              </div>
            ) : (
              <dl className="text-sm space-y-1.5">
                <div className="flex justify-between">
                  <dt className="text-[#8892a4]">Connector status</dt>
                  <dd className="text-white">{chargerStatus?.connectorStatus ?? '—'}</dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-[#8892a4]">Live power</dt>
                  <dd className="text-white">{formatKw(chargerStatus?.currentPowerKw)}</dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-[#8892a4]">Session energy</dt>
                  <dd className="text-white">{formatKwh(chargerStatus?.sessionEnergyKwh)}</dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-[#8892a4]">Session cost</dt>
                  <dd className="text-white">{formatMoney(chargerStatus?.sessionCost, priceSettings?.currency)}</dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-[#8892a4]">Last heartbeat</dt>
                  <dd className="text-white">{formatDateTime(chargerStatus?.lastHeartbeatAt)}</dd>
                </div>
              </dl>
            )}
            {chargerSettings && (
              <div className="space-y-1.5 border-t border-[#2a3042] pt-3">
                <div className="flex items-center justify-between text-sm">
                  <span className="text-[#8892a4]">Charge power</span>
                  <span className="flex items-center gap-1.5 text-white font-medium">
                    {setPower.isPending && <Loader2 className="h-3.5 w-3.5 animate-spin" />}
                    {formatKw(powerKw ?? chargerSettings.chargePowerSetpointKw)}
                  </span>
                </div>
                <input
                  type="range"
                  min={chargerSettings.chargePowerMinKw}
                  max={chargerSettings.chargePowerMaxKw}
                  step={0.1}
                  value={powerKw ?? chargerSettings.chargePowerSetpointKw}
                  disabled={!chargerStatus?.connected || setPower.isPending}
                  onChange={(e) => setPowerKw(Number(e.currentTarget.value))}
                  onMouseUp={(e) => handleSetPower(Number(e.currentTarget.value))}
                  onTouchEnd={(e) => handleSetPower(Number(e.currentTarget.value))}
                  onKeyUp={(e) => handleSetPower(Number(e.currentTarget.value))}
                  className="w-full accent-emerald-500 disabled:opacity-50 disabled:cursor-not-allowed"
                />
                <div className="flex justify-between text-xs text-[#8892a4]">
                  <span>{formatKw(chargerSettings.chargePowerMinKw)}</span>
                  <span>{formatKw(chargerSettings.chargePowerMaxKw)}</span>
                </div>
              </div>
            )}
            <div className="flex gap-2 pt-1">
              <button
                onClick={handleStart}
                disabled={startCharge.isPending}
                className="flex flex-1 items-center justify-center gap-1.5 rounded-md bg-emerald-600 px-3 py-2 text-sm font-medium text-white transition-colors hover:bg-emerald-500 disabled:opacity-50"
              >
                {startCharge.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Play className="h-4 w-4" />}
                Start
              </button>
              <button
                onClick={handleStop}
                disabled={stopCharge.isPending}
                className="flex flex-1 items-center justify-center gap-1.5 rounded-md bg-[#232938] border border-[#2a3042] px-3 py-2 text-sm font-medium text-white transition-colors hover:bg-[#2a3042] disabled:opacity-50"
              >
                {stopCharge.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Square className="h-4 w-4" />}
                Stop
              </button>
            </div>
            <div className="flex gap-2">
              {chargerStatus?.connectorStatus === 'Unavailable' ? (
                <button
                  onClick={() => handleSetAvailability(true)}
                  disabled={setAvailability.isPending}
                  className="flex flex-1 items-center justify-center gap-1.5 rounded-md bg-[#232938] border border-[#2a3042] px-3 py-2 text-sm font-medium text-white transition-colors hover:bg-[#2a3042] disabled:opacity-50"
                >
                  {setAvailability.isPending ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <CheckCircle className="h-4 w-4 text-emerald-400" />
                  )}
                  Set Available
                </button>
              ) : (
                <button
                  onClick={() => handleSetAvailability(false)}
                  disabled={setAvailability.isPending || chargerStatus?.connectorStatus !== 'Available'}
                  title={
                    chargerStatus?.connectorStatus !== 'Available'
                      ? 'Charger must be idle (Available) to set unavailable'
                      : undefined
                  }
                  className="flex flex-1 items-center justify-center gap-1.5 rounded-md bg-[#232938] border border-[#2a3042] px-3 py-2 text-sm font-medium text-white transition-colors hover:bg-[#2a3042] disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {setAvailability.isPending ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <XCircle className="h-4 w-4 text-red-400" />
                  )}
                  Set Unavailable
                </button>
              )}
            </div>
          </div>

          {/* Active plan card */}
          <div className="rounded-lg bg-[#1a1f2e] border border-[#2a3042] p-4 lg:col-span-2 flex flex-col gap-3">
            <div className="flex items-center justify-between gap-2">
              <h2 className="text-sm font-semibold text-white">Active Charge Plan</h2>
              <div className="flex items-center gap-2">
                {plan && (
                  <span className="text-xs text-[#8892a4]">
                    {formatDateTime(plan.deadlineUtc)}
                    {deadlineIn && ` · in ${deadlineIn}`}
                  </span>
                )}
                {plan && <Badge tone={PLAN_STATUS_TONE[plan.status]}>{plan.status}</Badge>}
              </div>
            </div>

            {!plan ? (
              <div className="flex flex-1 flex-col items-center justify-center py-6 text-[#8892a4]">
                <BatteryCharging className="h-8 w-8 mb-2 opacity-40" />
                <span className="text-sm">No active plan. Create one from the Schedule page.</span>
              </div>
            ) : (
              <>
                {/* SoC now → target */}
                <div className="space-y-1">
                  <div className="flex justify-between text-xs text-[#8892a4]">
                    <span>
                      Now{' '}
                      <span className="text-white font-medium">
                        {socPreview?.socPercent != null ? `${Math.round(socPreview.socPercent)}%` : '—'}
                      </span>
                    </span>
                    <span>
                      Target <span className="text-white font-medium">{plan.targetSocPercent}%</span>
                    </span>
                  </div>
                  <div className="relative h-1.5 w-full overflow-hidden rounded-full bg-[#232938]">
                    <div
                      className="h-full rounded-full bg-emerald-500 transition-all"
                      style={{ width: `${Math.min(100, Math.max(0, socPreview?.socPercent ?? 0))}%` }}
                    />
                    <div
                      className="absolute top-0 h-full w-px bg-white/60"
                      style={{ left: `${Math.min(100, Math.max(0, plan.targetSocPercent))}%` }}
                    />
                  </div>
                </div>

                {/* Compact stat strip */}
                <div className="grid grid-cols-3 divide-x divide-[#2a3042] rounded-md bg-[#151a26] text-center">
                  <div className="px-2 py-2">
                    <div className="text-[10px] uppercase tracking-wide text-[#8892a4]">Energy</div>
                    <div className="text-sm font-medium text-white">{formatKwh(plan.estimatedEnergyKwh)}</div>
                  </div>
                  <div className="px-2 py-2">
                    <div className="text-[10px] uppercase tracking-wide text-[#8892a4]">Est. cost</div>
                    <div className="text-sm font-medium text-white">
                      {formatMoney(plan.estimatedCost, priceSettings?.currency)}
                    </div>
                  </div>
                  <div className="px-2 py-2">
                    <div className="text-[10px] uppercase tracking-wide text-[#8892a4]">Avg price</div>
                    <div className="text-sm font-medium text-white">
                      {avgPricePerKwh != null
                        ? formatPricePerKwh(avgPricePerKwh, priceSettings?.currency)
                        : '—'}
                    </div>
                  </div>
                </div>

                {/* Selected charge hours */}
                <div className="flex-1 space-y-1.5">
                  <div className="text-[10px] uppercase tracking-wide text-[#8892a4]">
                    Charge hours ({plan.selectedHours.length})
                  </div>
                  {plan.selectedHours.length === 0 ? (
                    <span className="text-xs text-[#8892a4]">No hours selected — nothing left to charge.</span>
                  ) : (
                    <div className="flex flex-wrap gap-1">
                      {plan.selectedHours.map((hour) => {
                        const hourUtc = ensureUtcSuffix(hour)
                        const past = new Date(hourUtc).getTime() < currentHourStartMs
                        const current = new Date(hourUtc).getTime() === currentHourStartMs
                        const price = priceByHour.get(hourUtc)
                        return (
                          <span
                            key={hour}
                            title={price != null ? formatPricePerKwh(price, priceSettings?.currency) : undefined}
                            className={
                              'rounded px-1.5 py-0.5 text-xs tabular-nums border ' +
                              (current
                                ? 'border-emerald-500 bg-emerald-500/20 text-emerald-300'
                                : past
                                  ? 'border-[#2a3042] bg-[#151a26] text-[#5c6479] line-through'
                                  : 'border-[#2a3042] bg-[#232938] text-white')
                            }
                          >
                            {formatHourLabel(hour)}
                          </span>
                        )
                      })}
                    </div>
                  )}
                </div>

                {plan.status === 'MissedDeadline' && (
                  <div className="flex items-center gap-1.5 text-xs text-amber-400">
                    <AlertTriangle className="h-3.5 w-3.5" /> This plan missed its deadline.
                  </div>
                )}
              </>
            )}
          </div>
        </div>

        {/* Price chart */}
        <div className="rounded-lg bg-[#1a1f2e] border border-[#2a3042] p-4 space-y-3">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-semibold text-white">Electricity Price — Today &amp; Tomorrow</h2>
            <span className="text-xs text-[#8892a4]">
              {priceSettings?.currency ?? 'EUR'} / kWh
            </span>
          </div>
          {pricesLoading ? (
            <div className="flex items-center gap-2 py-10 justify-center text-sm text-[#8892a4]">
              <Loader2 className="h-4 w-4 animate-spin" /> Loading prices…
            </div>
          ) : (
            <PriceChart
              prices={prices ?? []}
              selectedHours={plan?.selectedHours}
              currency={priceSettings?.currency}
            />
          )}
        </div>
      </div>
    </div>
  )
}
