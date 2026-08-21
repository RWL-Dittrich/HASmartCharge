import { useEffect, useState } from 'react'
import { Loader2 } from 'lucide-react'
import { useChargerSettings, useUpdateChargerSettings } from '@/hooks/useSettings'
import { useReconfigureCharger, useSetChargerAvailability, useUnlockCharger } from '@/hooks/useCharger'
import { useZaptecChargers, useZaptecStatus } from '@/hooks/useZaptec'
import { NumberInput } from '@/components/ui/NumberInput'
import { CHARGE_POWER_UNITS } from '@/types/settings'
import { ensureUtcSuffix } from '@/lib/utils'
import type { ChargePowerControlMode, ChargePowerUnit, ChargerSettings, ChargerType } from '@/types/settings'
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

  const isZaptec = form?.chargerType === 'Zaptec'
  const { data: zaptecStatus } = useZaptecStatus(isZaptec)
  const {
    data: zaptecChargers,
    refetch: loadZaptecChargers,
    isFetching: loadingZaptecChargers,
    error: zaptecChargersError,
  } = useZaptecChargers()

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
      <label className="text-sm block">
        <span className="text-[#8892a4] block mb-1">Charger type</span>
        <select
          value={form.chargerType}
          onChange={(e) => setForm({ ...form, chargerType: e.target.value as ChargerType })}
          className="w-full max-w-xs rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
        >
          <option value="Ocpp">OCPP 1.6J</option>
          <option value="Zaptec">Zaptec cloud API</option>
        </select>
      </label>

      {isZaptec && (
        <div className="space-y-4 rounded-md border border-[#2a3042] p-4">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-[#8892a4]">Zaptec account</h3>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <label className="text-sm">
              <span className="text-[#8892a4] block mb-1">Username</span>
              <input
                value={form.zaptecUsername}
                onChange={(e) => setForm({ ...form, zaptecUsername: e.target.value })}
                className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
              />
            </label>
            <label className="text-sm">
              <span className="text-[#8892a4] block mb-1">Password</span>
              <input
                type="password"
                value={form.zaptecPassword}
                onChange={(e) => setForm({ ...form, zaptecPassword: e.target.value })}
                className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
              />
            </label>
            <label className="text-sm">
              <span className="text-[#8892a4] block mb-1">Poll interval (s)</span>
              <NumberInput
                min={1}
                value={form.zaptecPollSeconds}
                onChange={(v) => setForm({ ...form, zaptecPollSeconds: v })}
                className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
              />
            </label>
          </div>
          <div className="flex items-end gap-2">
            <label className="text-sm flex-1">
              <span className="text-[#8892a4] block mb-1">Charger</span>
              {zaptecChargers && zaptecChargers.length > 0 ? (
                <select
                  value={form.zaptecChargerId}
                  onChange={(e) => setForm({ ...form, zaptecChargerId: e.target.value })}
                  className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
                >
                  <option value="">Select a charger…</option>
                  {zaptecChargers.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name} ({c.deviceId ?? c.id})
                    </option>
                  ))}
                </select>
              ) : (
                <input
                  value={form.zaptecChargerId}
                  onChange={(e) => setForm({ ...form, zaptecChargerId: e.target.value })}
                  placeholder="Charger ID"
                  className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 font-mono text-sm text-white outline-none focus:border-blue-500"
                />
              )}
            </label>
            <button
              type="button"
              onClick={() => loadZaptecChargers()}
              disabled={loadingZaptecChargers}
              className="rounded-md border border-[#2a3042] px-3 py-2 text-sm text-white transition-colors hover:bg-[#1a1f2b] disabled:opacity-50"
            >
              {loadingZaptecChargers ? 'Loading…' : 'Load chargers'}
            </button>
          </div>
          {zaptecChargersError && (
            <p className="text-xs text-red-400">
              {zaptecChargersError instanceof ApiError ? zaptecChargersError.message : 'Loading chargers failed.'}
            </p>
          )}
          <p className="text-xs text-[#8892a4]">
            {zaptecStatus
              ? `${zaptecStatus.connected ? 'Connected' : 'Not connected'}${
                  zaptecStatus.lastPollAt ? ` · last poll ${new Date(ensureUtcSuffix(zaptecStatus.lastPollAt)).toLocaleTimeString()}` : ''
                }${zaptecStatus.lastError ? ` · ${zaptecStatus.lastError}` : ''}`
              : 'Status unavailable.'}
          </p>
        </div>
      )}

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        {!isZaptec && (
          <label className="text-sm">
            <span className="text-[#8892a4] block mb-1">Charge point ID</span>
            <input
              value={form.chargePointId}
              onChange={(e) => setForm({ ...form, chargePointId: e.target.value })}
              className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
            />
          </label>
        )}
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
          <NumberInput
            step="0.1"
            value={form.maxChargeKw}
            onChange={(v) => setForm({ ...form, maxChargeKw: v })}
            className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
          />
        </label>
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Connector ID</span>
          <NumberInput
            min={1}
            value={form.connectorId}
            onChange={(v) => setForm({ ...form, connectorId: v })}
            className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
          />
        </label>
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Charge power slider min (kW)</span>
          <NumberInput
            min={0}
            step="0.1"
            value={form.chargePowerMinKw}
            onChange={(v) => setForm({ ...form, chargePowerMinKw: v })}
            className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
          />
        </label>
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Charge power slider max (kW)</span>
          <NumberInput
            min={0}
            step="0.1"
            value={form.chargePowerMaxKw}
            onChange={(v) => setForm({ ...form, chargePowerMaxKw: v })}
            className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
          />
        </label>
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Supply voltage (V, per phase)</span>
          <NumberInput
            min={1}
            step="1"
            value={form.supplyVoltage}
            onChange={(v) => setForm({ ...form, supplyVoltage: v })}
            className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
          />
        </label>
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Number of phases</span>
          <NumberInput
            min={1}
            max={3}
            step="1"
            value={form.phaseCount}
            onChange={(v) => setForm({ ...form, phaseCount: v })}
            className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
          />
        </label>
      </div>
      <p className="text-xs text-[#8892a4]">
        Bounds for the charge-power slider on the dashboard. The slider always works in kW
        {isZaptec ? '; Zaptec applies it as a max charge current (A).' : '; how that setpoint reaches the charger is chosen below.'}
      </p>

      {!isZaptec && (
      <div className="border-t border-[#2a3042] pt-4 space-y-4">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-[#8892a4]">
          Power control method
        </h3>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
          <label className="text-sm sm:col-span-3">
            <span className="text-[#8892a4] block mb-1">Method</span>
            <select
              value={form.chargePowerControlMode}
              onChange={(e) =>
                setForm({ ...form, chargePowerControlMode: e.target.value as ChargePowerControlMode })
              }
              className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
            >
              <option value="ChargingProfile">SetChargingProfile (smart charging)</option>
              <option value="Configuration">ChangeConfiguration (vendor key, e.g. USER_PMAX)</option>
            </select>
          </label>
          {form.chargePowerControlMode === 'Configuration' && (
            <>
              <label className="text-sm sm:col-span-2">
                <span className="text-[#8892a4] block mb-1">Configuration key</span>
                <input
                  value={form.chargePowerConfigurationKey}
                  onChange={(e) => setForm({ ...form, chargePowerConfigurationKey: e.target.value })}
                  placeholder="USER_PMAX"
                  className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 font-mono text-sm text-white outline-none focus:border-blue-500"
                />
              </label>
              <label className="text-sm">
                <span className="text-[#8892a4] block mb-1">Unit</span>
                <select
                  value={form.chargePowerConfigurationUnit}
                  onChange={(e) =>
                    setForm({ ...form, chargePowerConfigurationUnit: e.target.value as ChargePowerUnit })
                  }
                  className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
                >
                  {CHARGE_POWER_UNITS.map((u) => (
                    <option key={u} value={u}>
                      {u}
                    </option>
                  ))}
                </select>
              </label>
            </>
          )}
        </div>
        <p className="text-xs text-[#8892a4]">
          {form.chargePowerControlMode === 'Configuration'
            ? 'The slider value is converted to the selected unit (A and mA use A = W ÷ (phases × voltage)), rounded down, and written to the key with ChangeConfiguration.'
            : 'The backend converts kW to amps (A = W ÷ (phases × voltage)) and sends an OCPP SetChargingProfile to cap delivered current. The charger must support smart charging.'}
        </p>
      </div>
      )}

      {!isZaptec && (
      <div className="border-t border-[#2a3042] pt-4 space-y-4">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-[#8892a4]">
          On-connect configuration
        </h3>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
          <label className="text-sm">
            <span className="text-[#8892a4] block mb-1">Heartbeat interval (s)</span>
            <NumberInput
              min={1}
              value={form.heartbeatInterval}
              onChange={(v) => setForm({ ...form, heartbeatInterval: v })}
              className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
            />
          </label>
          <label className="text-sm">
            <span className="text-[#8892a4] block mb-1">Meter sample interval (s)</span>
            <NumberInput
              min={1}
              value={form.meterValueSampleInterval}
              onChange={(v) => setForm({ ...form, meterValueSampleInterval: v })}
              className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
            />
          </label>
          <label className="text-sm">
            <span className="text-[#8892a4] block mb-1">Clock-aligned interval (s)</span>
            <NumberInput
              min={1}
              value={form.clockAlignedDataInterval}
              onChange={(v) => setForm({ ...form, clockAlignedDataInterval: v })}
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
      )}

      {saveError && <div className="text-sm text-red-400">{saveError}</div>}
      {savedAt && !saveError && <div className="text-sm text-emerald-400">Saved.</div>}

      <button
        onClick={handleSave}
        disabled={updateSettings.isPending}
        className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-500 disabled:opacity-50"
      >
        {updateSettings.isPending ? 'Saving…' : 'Save'}
      </button>

      {!isZaptec && (
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
      )}
    </div>
  )
}
