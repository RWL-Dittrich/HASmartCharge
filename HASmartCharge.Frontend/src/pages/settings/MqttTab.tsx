import { useEffect, useState } from 'react'
import { CheckCircle2, Loader2, Wifi, WifiOff, XCircle } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { useMqttSettings, useUpdateMqttSettings } from '@/hooks/useSettings'
import { useMqttStatus, useTestMqtt } from '@/hooks/useMqtt'
import type { MqttSettings } from '@/types/settings'
import { ApiError } from '@/api/client'
import { formatDateTime } from '@/lib/utils'

const inputClass =
  'w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500'

export function MqttTab() {
  const { data: settings, isLoading } = useMqttSettings()
  const { data: status } = useMqttStatus()
  const updateSettings = useUpdateMqttSettings()
  const test = useTestMqtt()

  const [form, setForm] = useState<MqttSettings | null>(null)
  const [saveError, setSaveError] = useState<string | null>(null)
  const [savedAt, setSavedAt] = useState<number | null>(null)

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
      setSaveError(err instanceof ApiError ? err.message : 'Failed to save MQTT settings')
    }
  }

  return (
    <div className="max-w-2xl space-y-5">
      {/* Live publisher status */}
      <div className="flex items-center justify-between rounded-lg border border-[#2a3042] bg-[#0f1117] p-4">
        <div>
          <div className="flex items-center gap-2">
            <span className="text-sm font-medium text-white">Publisher status</span>
            {status?.connected ? (
              <Badge tone="success" pulse>
                <Wifi className="h-3 w-3" /> Connected
              </Badge>
            ) : (
              <Badge tone="danger">
                <WifiOff className="h-3 w-3" /> Disconnected
              </Badge>
            )}
          </div>
          <div className="mt-1 space-y-0.5 text-xs text-[#8892a4]">
            {status && (
              <div>
                {status.host}:{status.port}
                {!status.enabled && ' · disabled'}
              </div>
            )}
            {status?.lastPublishAt && <div>Last publish {formatDateTime(status.lastPublishAt)}</div>}
            {status?.lastError && <div className="text-red-400">{status.lastError}</div>}
          </div>
        </div>
      </div>

      {/* Settings form */}
      <label className="flex items-center gap-2 text-sm text-[#c3cad8]">
        <input
          type="checkbox"
          checked={form.enabled}
          onChange={(e) => setForm({ ...form, enabled: e.target.checked })}
          className="h-4 w-4 accent-blue-600"
        />
        Enabled
      </label>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Broker host</span>
          <input value={form.host} onChange={(e) => setForm({ ...form, host: e.target.value })} className={inputClass} />
        </label>
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Port</span>
          <input
            type="number"
            min={1}
            value={form.port}
            onChange={(e) => setForm({ ...form, port: Number(e.target.value) })}
            className={inputClass}
          />
        </label>
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Username</span>
          <input
            value={form.username ?? ''}
            onChange={(e) => setForm({ ...form, username: e.target.value })}
            className={inputClass}
          />
        </label>
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Password</span>
          <input
            type="password"
            value={form.password ?? ''}
            onChange={(e) => setForm({ ...form, password: e.target.value })}
            className={inputClass}
          />
        </label>
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Client ID</span>
          <input
            value={form.clientId}
            onChange={(e) => setForm({ ...form, clientId: e.target.value })}
            className={inputClass}
          />
        </label>
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Base topic</span>
          <input
            value={form.baseTopic}
            onChange={(e) => setForm({ ...form, baseTopic: e.target.value })}
            className={inputClass}
          />
        </label>
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Discovery prefix</span>
          <input
            value={form.discoveryPrefix}
            onChange={(e) => setForm({ ...form, discoveryPrefix: e.target.value })}
            className={inputClass}
          />
        </label>
      </div>

      <label className="flex items-center gap-2 text-sm text-[#c3cad8]">
        <input
          type="checkbox"
          checked={form.useTls}
          onChange={(e) => setForm({ ...form, useTls: e.target.checked })}
          className="h-4 w-4 accent-blue-600"
        />
        Use TLS
      </label>

      {saveError && <div className="text-sm text-red-400">{saveError}</div>}
      {savedAt && !saveError && <div className="text-sm text-emerald-400">Saved.</div>}

      <div className="flex flex-wrap items-center gap-2">
        <button
          onClick={handleSave}
          disabled={updateSettings.isPending}
          className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-500 disabled:opacity-50"
        >
          {updateSettings.isPending ? 'Saving…' : 'Save'}
        </button>
        <button
          onClick={() => test.mutate()}
          disabled={test.isPending}
          className="flex items-center gap-1.5 rounded-md border border-[#2a3042] bg-[#232938] px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-[#2a3042] disabled:opacity-50"
        >
          {test.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
          Test connection
        </button>
      </div>

      <p className="text-xs text-[#8892a4]">Tests the last saved settings.</p>

      {(test.data || test.isError) && (
        <div
          className={`flex items-start gap-2 rounded-md border px-3 py-2 text-sm ${
            test.data?.success
              ? 'border-emerald-500/30 bg-emerald-500/10 text-emerald-400'
              : 'border-red-500/30 bg-red-500/10 text-red-400'
          }`}
        >
          {test.data?.success ? (
            <CheckCircle2 className="h-4 w-4 shrink-0 mt-0.5" />
          ) : (
            <XCircle className="h-4 w-4 shrink-0 mt-0.5" />
          )}
          <div>
            {test.data?.success
              ? 'Connected to the broker successfully.'
              : test.data?.error ??
                (test.error instanceof ApiError ? test.error.message : 'Test failed.')}
          </div>
        </div>
      )}
    </div>
  )
}
