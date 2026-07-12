import { useEffect, useState } from 'react'
import { Loader2 } from 'lucide-react'
import { useChargerSettings, useUpdateChargerSettings } from '@/hooks/useSettings'
import { useReconfigureCharger, useSetChargerAvailability, useUnlockCharger } from '@/hooks/useCharger'
import type { ChargerSettings } from '@/types/settings'
import { ApiError } from '@/api/client'

function ResultBanner({ label, error }: { label: string; error?: string | null }) {
  return (
    <div
      className={`rounded-md border px-3 py-2 text-xs ${
        error
          ? 'border-red-500/30 bg-red-500/10 text-red-400'
          : 'border-emerald-500/30 bg-emerald-500/10 text-emerald-400'
      }`}
    >
      {error ?? label}
    </div>
  )
}

export function ChargerTab() {
  const { data: settings, isLoading } = useChargerSettings()
  const updateSettings = useUpdateChargerSettings()

  const unlock = useUnlockCharger()
  const setAvailability = useSetChargerAvailability()
  const reconfigure = useReconfigureCharger()

  const [form, setForm] = useState<ChargerSettings | null>(null)
  const [saveError, setSaveError] = useState<string | null>(null)
  const [savedAt, setSavedAt] = useState<number | null>(null)
  const [actionResult, setActionResult] = useState<{ label: string; error?: string } | null>(null)

  useEffect(() => {
    if (settings && !form) setForm(settings)
  }, [settings, form])

  if (isLoading || !form) {
    return (
      <div className="flex items-center gap-2 text-sm text-[#8892a4] py-8">
        <Loader2 className="h-4 w-4 animate-spin" /> Loading…
      </div>
    )
  }

  async function handleSave() {
    if (!form) return
    setSaveError(null)
    try {
      const saved = await updateSettings.mutateAsync(form)
      setForm(saved)
      setSavedAt(Date.now())
    } catch (err) {
      setSaveError(err instanceof ApiError ? err.message : 'Failed to save charger settings')
    }
  }

  async function runAction(label: string, action: () => Promise<unknown>) {
    setActionResult(null)
    try {
      await action()
      setActionResult({ label: `${label} succeeded.` })
    } catch (err) {
      setActionResult({ label, error: err instanceof ApiError ? err.message : `${label} failed.` })
    }
  }

  return (
    <div className="space-y-5 max-w-2xl">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Charge point ID</span>
          <input
            value={form.chargePointId}
            onChange={(e) => setForm({ ...form, chargePointId: e.target.value })}
            className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
          />
        </label>
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Friendly name</span>
          <input
            value={form.friendlyName}
            onChange={(e) => setForm({ ...form, friendlyName: e.target.value })}
            className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
          />
        </label>
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Max charge power (kW)</span>
          <input
            type="number"
            step="0.1"
            value={form.maxChargeKw}
            onChange={(e) => setForm({ ...form, maxChargeKw: Number(e.target.value) })}
            className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
          />
        </label>
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Connector ID</span>
          <input
            type="number"
            min={1}
            value={form.connectorId}
            onChange={(e) => setForm({ ...form, connectorId: Number(e.target.value) })}
            className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
          />
        </label>
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Charge power slider min (kW)</span>
          <input
            type="number"
            min={0}
            step="0.1"
            value={form.chargePowerMinKw}
            onChange={(e) => setForm({ ...form, chargePowerMinKw: Number(e.target.value) })}
            className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
          />
        </label>
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Charge power slider max (kW)</span>
          <input
            type="number"
            min={0}
            step="0.1"
            value={form.chargePowerMaxKw}
            onChange={(e) => setForm({ ...form, chargePowerMaxKw: Number(e.target.value) })}
            className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
          />
        </label>
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Supply voltage (V, per phase)</span>
          <input
            type="number"
            min={1}
            step="1"
            value={form.supplyVoltage}
            onChange={(e) => setForm({ ...form, supplyVoltage: Number(e.target.value) })}
            className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
          />
        </label>
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Number of phases</span>
          <input
            type="number"
            min={1}
            max={3}
            step="1"
            value={form.phaseCount}
            onChange={(e) => setForm({ ...form, phaseCount: Number(e.target.value) })}
            className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
          />
        </label>
      </div>
      <p className="text-xs text-[#8892a4]">
        Bounds for the charge-power slider on the dashboard. The slider works in kW; the backend
        converts to amps (A = W ÷ (phases × voltage)) and sends an OCPP SetChargingProfile in amps to
        cap delivered current. The charger must support smart charging.
      </p>

      <div className="border-t border-[#2a3042] pt-4 space-y-4">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-[#8892a4]">
          On-connect configuration
        </h3>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
          <label className="text-sm">
            <span className="text-[#8892a4] block mb-1">Heartbeat interval (s)</span>
            <input
              type="number"
              min={1}
              value={form.heartbeatInterval}
              onChange={(e) => setForm({ ...form, heartbeatInterval: Number(e.target.value) })}
              className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
            />
          </label>
          <label className="text-sm">
            <span className="text-[#8892a4] block mb-1">Meter sample interval (s)</span>
            <input
              type="number"
              min={1}
              value={form.meterValueSampleInterval}
              onChange={(e) => setForm({ ...form, meterValueSampleInterval: Number(e.target.value) })}
              className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
            />
          </label>
          <label className="text-sm">
            <span className="text-[#8892a4] block mb-1">Clock-aligned interval (s)</span>
            <input
              type="number"
              min={1}
              value={form.clockAlignedDataInterval}
              onChange={(e) => setForm({ ...form, clockAlignedDataInterval: Number(e.target.value) })}
              className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
            />
          </label>
        </div>
        <label className="text-sm block">
          <span className="text-[#8892a4] block mb-1">Sampled measurands (CSV)</span>
          <textarea
            value={form.meterValuesSampledData}
            onChange={(e) => setForm({ ...form, meterValuesSampledData: e.target.value })}
            rows={2}
            className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 font-mono text-xs text-white outline-none focus:border-blue-500"
          />
        </label>
      </div>

      {saveError && <div className="text-sm text-red-400">{saveError}</div>}
      {savedAt && !saveError && <div className="text-sm text-emerald-400">Saved.</div>}

      <button
        onClick={handleSave}
        disabled={updateSettings.isPending}
        className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-500 disabled:opacity-50"
      >
        {updateSettings.isPending ? 'Saving…' : 'Save'}
      </button>

      <div className="border-t border-[#2a3042] pt-4 space-y-3">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-[#8892a4]">Charger commands</h3>
        <div className="flex flex-wrap gap-2">
          <button
            onClick={() => runAction('Unlock connector', () => unlock.mutateAsync())}
            disabled={unlock.isPending}
            className="rounded-md border border-[#2a3042] bg-[#232938] px-3 py-2 text-sm font-medium text-white transition-colors hover:bg-[#2a3042] disabled:opacity-50"
          >
            {unlock.isPending ? 'Unlocking…' : 'Unlock connector'}
          </button>
          <button
            onClick={() => runAction('Set available', () => setAvailability.mutateAsync(true))}
            disabled={setAvailability.isPending}
            className="rounded-md border border-[#2a3042] bg-[#232938] px-3 py-2 text-sm font-medium text-white transition-colors hover:bg-[#2a3042] disabled:opacity-50"
          >
            Set available
          </button>
          <button
            onClick={() => runAction('Set inoperative', () => setAvailability.mutateAsync(false))}
            disabled={setAvailability.isPending}
            className="rounded-md border border-[#2a3042] bg-[#232938] px-3 py-2 text-sm font-medium text-white transition-colors hover:bg-[#2a3042] disabled:opacity-50"
          >
            Set inoperative
          </button>
          <button
            onClick={() => runAction('Re-push configuration', () => reconfigure.mutateAsync())}
            disabled={reconfigure.isPending}
            className="rounded-md border border-[#2a3042] bg-[#232938] px-3 py-2 text-sm font-medium text-white transition-colors hover:bg-[#2a3042] disabled:opacity-50"
          >
            {reconfigure.isPending ? 'Pushing…' : 'Re-push config'}
          </button>
        </div>
        {actionResult && <ResultBanner label={actionResult.label} error={actionResult.error} />}
      </div>
    </div>
  )
}
