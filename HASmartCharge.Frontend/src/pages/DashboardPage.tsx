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
import { formatDateTime, formatKw, formatKwh, formatMoney } from '@/lib/utils'
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
            value={chargerStatus?.connected ? 'Connected' : 'Offline'}
            change={chargerStatus?.connectorStatus ?? undefined}
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
          <div className="rounded-lg bg-[#1a1f2e] border border-[#2a3042] p-4 space-y-3 lg:col-span-2">
            <div className="flex items-center justify-between">
              <h2 className="text-sm font-semibold text-white">Active Charge Plan</h2>
              {plan && <Badge tone={PLAN_STATUS_TONE[plan.status]}>{plan.status}</Badge>}
            </div>

            {!plan ? (
              <div className="flex flex-col items-center justify-center py-8 text-[#8892a4]">
                <BatteryCharging className="h-8 w-8 mb-2 opacity-40" />
                <span className="text-sm">No active plan. Create one from the Schedule page.</span>
              </div>
            ) : (
              <div className="grid grid-cols-2 gap-3 text-sm sm:grid-cols-4">
                <div>
                  <div className="text-xs text-[#8892a4] uppercase tracking-wide">Target</div>
                  <div className="text-white font-medium">{plan.targetSocPercent}%</div>
                </div>
                <div>
                  <div className="text-xs text-[#8892a4] uppercase tracking-wide">Deadline</div>
                  <div className="text-white font-medium">{formatDateTime(plan.deadlineUtc)}</div>
                </div>
                <div>
                  <div className="text-xs text-[#8892a4] uppercase tracking-wide">Estimated Cost</div>
                  <div className="text-white font-medium">
                    {formatMoney(plan.estimatedCost, priceSettings?.currency)}
                  </div>
                </div>
                <div>
                  <div className="text-xs text-[#8892a4] uppercase tracking-wide">Energy Needed</div>
                  <div className="text-white font-medium">{formatKwh(plan.estimatedEnergyKwh)}</div>
                </div>
                {plan.status === 'MissedDeadline' && (
                  <div className="col-span-2 flex items-center gap-1.5 text-amber-400 sm:col-span-4">
                    <AlertTriangle className="h-3.5 w-3.5" /> This plan missed its deadline.
                  </div>
                )}
              </div>
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
