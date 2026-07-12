import { useEffect, useState } from 'react'
import { CheckCircle2, Loader2, RefreshCw, XCircle } from 'lucide-react'
import { usePriceSettings, useUpdatePriceSettings } from '@/hooks/useSettings'
import { useRefreshPrices } from '@/hooks/usePrices'
import type { PriceProviderSettings } from '@/types/settings'
import { ApiError } from '@/api/client'

export function PriceProviderTab() {
  const { data: settings, isLoading } = usePriceSettings()
  const updateSettings = useUpdatePriceSettings()
  const refreshPrices = useRefreshPrices()

  const [form, setForm] = useState<PriceProviderSettings | null>(null)
  const [saveError, setSaveError] = useState<string | null>(null)
  const [savedAt, setSavedAt] = useState<number | null>(null)

  useEffect(() => {
    if (settings && !form) setForm(settings)
  }, [settings, form])

  async function handleSave() {
    if (!form) return
    setSaveError(null)
    try {
      const saved = await updateSettings.mutateAsync(form)
      setForm(saved)
      setSavedAt(Date.now())
    } catch (err) {
      setSaveError(err instanceof ApiError ? err.message : 'Failed to save price settings')
    }
  }

  if (isLoading || !form) {
    return (
      <div className="flex items-center gap-2 text-sm text-[#8892a4] py-8">
        <Loader2 className="h-4 w-4 animate-spin" /> Loading…
      </div>
    )
  }

  return (
    <div className="space-y-4 max-w-xl">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Supplier slug</span>
          <input
            value={form.supplierSlug}
            onChange={(e) => setForm({ ...form, supplierSlug: e.target.value })}
            className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
          />
        </label>
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Currency</span>
          <input
            value={form.currency}
            onChange={(e) => setForm({ ...form, currency: e.target.value })}
            className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
          />
        </label>
        <label className="text-sm sm:col-span-2">
          <span className="text-[#8892a4] block mb-1">API URL</span>
          <input
            value={form.apiUrl}
            onChange={(e) => setForm({ ...form, apiUrl: e.target.value })}
            className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
          />
        </label>
        <label className="text-sm">
          <span className="text-[#8892a4] block mb-1">Refresh interval (minutes)</span>
          <input
            type="number"
            min={1}
            value={form.refreshMinutes}
            onChange={(e) => setForm({ ...form, refreshMinutes: Number(e.target.value) })}
            className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
          />
        </label>
      </div>

      {saveError && <div className="text-sm text-red-400">{saveError}</div>}
      {savedAt && !saveError && <div className="text-sm text-emerald-400">Saved.</div>}

      <div className="flex flex-wrap items-center gap-3 pt-1">
        <button
          onClick={handleSave}
          disabled={updateSettings.isPending}
          className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-500 disabled:opacity-50"
        >
          {updateSettings.isPending ? 'Saving…' : 'Save'}
        </button>
        <button
          onClick={() => refreshPrices.mutate()}
          disabled={refreshPrices.isPending}
          className="flex items-center gap-1.5 rounded-md border border-[#2a3042] bg-[#232938] px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-[#2a3042] disabled:opacity-50"
        >
          {refreshPrices.isPending ? (
            <Loader2 className="h-4 w-4 animate-spin" />
          ) : (
            <RefreshCw className="h-4 w-4" />
          )}
          Refresh now
        </button>
      </div>

      {refreshPrices.data && (
        <div
          className={`flex items-start gap-2 rounded-md border px-3 py-2 text-sm ${
            refreshPrices.data.success
              ? 'border-emerald-500/30 bg-emerald-500/10 text-emerald-400'
              : 'border-red-500/30 bg-red-500/10 text-red-400'
          }`}
        >
          {refreshPrices.data.success ? (
            <CheckCircle2 className="h-4 w-4 shrink-0 mt-0.5" />
          ) : (
            <XCircle className="h-4 w-4 shrink-0 mt-0.5" />
          )}
          <div>
            <div>
              {refreshPrices.data.success
                ? `Upserted ${refreshPrices.data.pricesUpserted} price(s).`
                : refreshPrices.data.error ?? 'Refresh failed.'}
            </div>
            <div className="text-xs opacity-80">
              Tomorrow's prices {refreshPrices.data.tomorrowAvailable ? 'available' : 'not yet available'}.
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
