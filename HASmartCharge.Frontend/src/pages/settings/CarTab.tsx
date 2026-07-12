import { useId, useEffect, useState } from 'react'
import { Loader2 } from 'lucide-react'
import { useCarSettings, useUpdateCarSettings } from '@/hooks/useSettings'
import { useHaServices } from '@/hooks/useHa'
import { EntityPicker } from '@/components/settings/EntityPicker'
import type { CarSettings } from '@/types/settings'
import { ApiError } from '@/api/client'

function isValidJsonOrEmpty(value: string | null): boolean {
  if (!value || value.trim() === '') return true
  try {
    JSON.parse(value)
    return true
  } catch {
    return false
  }
}

/** Entity domains that make sense as start/stop charging actuators, for the "fill from entity" picker. */
const ACTUATOR_DOMAINS = [
  'button',
  'switch',
  'script',
  'input_boolean',
  'automation',
  'number',
  'select',
  'light',
  'climate',
]

/** Derives a service call (domain, service, data) for starting/stopping charging from a picked entity. */
function deriveServiceCall(entityId: string, isStart: boolean): { domain: string; service: string; data: Record<string, unknown> } {
  const domain = entityId.split('.')[0]
  const data: Record<string, unknown> = { entity_id: entityId }

  switch (domain) {
    case 'button':
      return { domain, service: 'press', data }
    case 'script':
      return { domain, service: 'turn_on', data }
    case 'automation':
      return { domain, service: 'trigger', data }
    case 'number':
      return { domain, service: 'set_value', data: { ...data, value: 0 } }
    case 'select':
      return { domain, service: 'select_option', data: { ...data, option: '' } }
    case 'switch':
    case 'input_boolean':
    case 'light':
    case 'climate':
    default:
      return { domain, service: isStart ? 'turn_on' : 'turn_off', data }
  }
}

export function CarTab() {
  const { data: settings, isLoading } = useCarSettings()
  const updateSettings = useUpdateCarSettings()
  const { data: serviceDomains } = useHaServices()

  const [form, setForm] = useState<CarSettings | null>(null)
  const [saveError, setSaveError] = useState<string | null>(null)
  const [savedAt, setSavedAt] = useState<number | null>(null)
  const [startFillEntityId, setStartFillEntityId] = useState('')
  const [stopFillEntityId, setStopFillEntityId] = useState('')

  const startDomainListId = useId()
  const startServiceListId = useId()
  const stopDomainListId = useId()
  const stopServiceListId = useId()

  const startServices = serviceDomains?.find((d) => d.domain === form?.haStartDomain)?.services
  const stopServices = serviceDomains?.find((d) => d.domain === form?.haStopDomain)?.services

  function fillStart() {
    if (!form || !startFillEntityId) return
    const { domain, service, data } = deriveServiceCall(startFillEntityId, true)
    setForm({
      ...form,
      haStartDomain: domain,
      haStartService: service,
      haStartDataJson: JSON.stringify(data, null, 2),
    })
  }

  function fillStop() {
    if (!form || !stopFillEntityId) return
    const { domain, service, data } = deriveServiceCall(stopFillEntityId, false)
    setForm({
      ...form,
      haStopDomain: domain,
      haStopService: service,
      haStopDataJson: JSON.stringify(data, null, 2),
    })
  }

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

  const startJsonValid = isValidJsonOrEmpty(form.haStartDataJson)
  const stopJsonValid = isValidJsonOrEmpty(form.haStopDataJson)
  const canSave = startJsonValid && stopJsonValid

  async function handleSave() {
    if (!form || !canSave) return
    setSaveError(null)
    try {
      const saved = await updateSettings.mutateAsync(form)
      setForm(saved)
      setSavedAt(Date.now())
    } catch (err) {
      setSaveError(err instanceof ApiError ? err.message : 'Failed to save car settings')
    }
  }

  return (
    <div className="space-y-5 max-w-2xl">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Name</span>
          <input
            value={form.name}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
            className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
          />
        </label>
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Battery capacity (kWh)</span>
          <input
            type="number"
            step="0.1"
            value={form.batteryCapacityKwh}
            onChange={(e) => setForm({ ...form, batteryCapacityKwh: Number(e.target.value) })}
            className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
          />
        </label>
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Default target SoC (%)</span>
          <input
            type="number"
            min={1}
            max={100}
            value={form.targetSocPercent}
            onChange={(e) => setForm({ ...form, targetSocPercent: Number(e.target.value) })}
            className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
          />
        </label>
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Charge efficiency (0–1)</span>
          <input
            type="number"
            step="0.01"
            min={0}
            max={1}
            value={form.chargeEfficiency}
            onChange={(e) => setForm({ ...form, chargeEfficiency: Number(e.target.value) })}
            className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
          />
        </label>
      </div>

      <div className="border-t border-[#2a3042] pt-4 space-y-4">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-[#8892a4]">Home Assistant entities</h3>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <EntityPicker
            label="Battery SoC sensor"
            domain="sensor"
            value={form.haSocEntityId}
            onChange={(v) => setForm({ ...form, haSocEntityId: v })}
          />
          <EntityPicker
            label="Plugged-in sensor (optional)"
            domain="binary_sensor"
            value={form.haPluggedInEntityId ?? ''}
            onChange={(v) => setForm({ ...form, haPluggedInEntityId: v || null })}
          />
          <EntityPicker
            label="Charging state entity (optional)"
            value={form.haChargingStateEntityId ?? ''}
            onChange={(v) => setForm({ ...form, haChargingStateEntityId: v || null })}
          />
          <EntityPicker
            label="Target SoC entity (optional)"
            value={form.haTargetSocEntityId ?? ''}
            onChange={(v) => setForm({ ...form, haTargetSocEntityId: v || null })}
          />
        </div>
      </div>

      <div className="border-t border-[#2a3042] pt-4 space-y-4">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-[#8892a4]">Start charging service</h3>
        <div className="flex items-end gap-2">
          <div className="flex-1">
            <EntityPicker
              label="Fill from entity"
              domains={ACTUATOR_DOMAINS}
              value={startFillEntityId}
              onChange={setStartFillEntityId}
              placeholder="button.car_charger_start"
            />
          </div>
          <button
            type="button"
            onClick={fillStart}
            disabled={!startFillEntityId}
            className="rounded-md border border-[#2a3042] px-3 py-2 text-sm text-white transition-colors hover:bg-[#1a1f2b] disabled:opacity-50"
          >
            Fill
          </button>
        </div>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <label className="text-sm">
            <span className="text-[#8892a4] block mb-1">Domain</span>
            <input
              list={startDomainListId}
              value={form.haStartDomain}
              onChange={(e) => setForm({ ...form, haStartDomain: e.target.value })}
              placeholder="switch"
              className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
            />
            <datalist id={startDomainListId}>
              {serviceDomains?.map((d) => <option key={d.domain} value={d.domain} />)}
            </datalist>
          </label>
          <label className="text-sm">
            <span className="text-[#8892a4] block mb-1">Service</span>
            <input
              list={startServiceListId}
              value={form.haStartService}
              onChange={(e) => setForm({ ...form, haStartService: e.target.value })}
              placeholder="turn_on"
              className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
            />
            <datalist id={startServiceListId}>
              {startServices?.map((s) => <option key={s} value={s} />)}
            </datalist>
          </label>
        </div>
        <label className="text-sm block">
          <span className="text-[#8892a4] block mb-1">Data (JSON, optional)</span>
          <textarea
            value={form.haStartDataJson ?? ''}
            onChange={(e) => setForm({ ...form, haStartDataJson: e.target.value || null })}
            rows={2}
            placeholder='{"entity_id": "switch.car_charger"}'
            className={`w-full rounded-md border bg-[#0f1117] px-3 py-2 font-mono text-xs text-white outline-none focus:border-blue-500 ${
              startJsonValid ? 'border-[#2a3042]' : 'border-red-500'
            }`}
          />
          {!startJsonValid && <span className="text-xs text-red-400">Invalid JSON</span>}
        </label>
      </div>

      <div className="border-t border-[#2a3042] pt-4 space-y-4">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-[#8892a4]">Stop charging service</h3>
        <div className="flex items-end gap-2">
          <div className="flex-1">
            <EntityPicker
              label="Fill from entity"
              domains={ACTUATOR_DOMAINS}
              value={stopFillEntityId}
              onChange={setStopFillEntityId}
              placeholder="button.car_charger_stop"
            />
          </div>
          <button
            type="button"
            onClick={fillStop}
            disabled={!stopFillEntityId}
            className="rounded-md border border-[#2a3042] px-3 py-2 text-sm text-white transition-colors hover:bg-[#1a1f2b] disabled:opacity-50"
          >
            Fill
          </button>
        </div>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <label className="text-sm">
            <span className="text-[#8892a4] block mb-1">Domain</span>
            <input
              list={stopDomainListId}
              value={form.haStopDomain}
              onChange={(e) => setForm({ ...form, haStopDomain: e.target.value })}
              placeholder="switch"
              className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
            />
            <datalist id={stopDomainListId}>
              {serviceDomains?.map((d) => <option key={d.domain} value={d.domain} />)}
            </datalist>
          </label>
          <label className="text-sm">
            <span className="text-[#8892a4] block mb-1">Service</span>
            <input
              list={stopServiceListId}
              value={form.haStopService}
              onChange={(e) => setForm({ ...form, haStopService: e.target.value })}
              placeholder="turn_off"
              className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
            />
            <datalist id={stopServiceListId}>
              {stopServices?.map((s) => <option key={s} value={s} />)}
            </datalist>
          </label>
        </div>
        <label className="text-sm block">
          <span className="text-[#8892a4] block mb-1">Data (JSON, optional)</span>
          <textarea
            value={form.haStopDataJson ?? ''}
            onChange={(e) => setForm({ ...form, haStopDataJson: e.target.value || null })}
            rows={2}
            placeholder='{"entity_id": "switch.car_charger"}'
            className={`w-full rounded-md border bg-[#0f1117] px-3 py-2 font-mono text-xs text-white outline-none focus:border-blue-500 ${
              stopJsonValid ? 'border-[#2a3042]' : 'border-red-500'
            }`}
          />
          {!stopJsonValid && <span className="text-xs text-red-400">Invalid JSON</span>}
        </label>
      </div>

      {saveError && <div className="text-sm text-red-400">{saveError}</div>}
      {savedAt && !saveError && <div className="text-sm text-emerald-400">Saved.</div>}

      <button
        onClick={handleSave}
        disabled={updateSettings.isPending || !canSave}
        className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-500 disabled:opacity-50"
      >
        {updateSettings.isPending ? 'Saving…' : 'Save'}
      </button>
    </div>
  )
}
