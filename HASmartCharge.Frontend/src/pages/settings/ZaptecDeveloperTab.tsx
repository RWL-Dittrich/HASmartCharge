import { lazy, Suspense, useState } from 'react'
import Editor from 'react-simple-code-editor'
import { AlertTriangle, Loader2 } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { useSendZaptecApiCall } from '@/hooks/useZaptec'
import { useChargerSettings } from '@/hooks/useSettings'
import { cn } from '@/lib/utils'
import { highlightJson, tryFormatJson } from '@/lib/highlightJson'
import { ApiError } from '@/api/client'
import { JsonBlock, Panel } from './OcppDeveloperTab'
import { DOCUMENTED_ZAPTEC_CALLS } from './zaptecDocs'

// Lazy for the same reason as OcppDocPanel: react-markdown stays out of the main bundle.
const ZaptecDocPanel = lazy(() =>
  import('./ZaptecDocPanel').then((m) => ({ default: m.ZaptecDocPanel })),
)

const inputClass =
  'w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500'

type ZaptecMethod = 'GET' | 'POST' | 'PUT' | 'DELETE'

interface ZaptecTemplate {
  /** Doc slug — matches ./zaptec-docs/<slug>.md. */
  slug: string
  label: string
  method: ZaptecMethod
  // {chargerId} is substituted with the configured Zaptec charger GUID at render time.
  path: string
  body?: object
}

// The useful corners of the Zaptec REST API (https://docs.zaptec.com/reference/). Everything
// else is reachable by editing the path — the backend forwards any /api/... request verbatim.
const ZAPTEC_TEMPLATES: ZaptecTemplate[] = [
  { slug: 'ListChargers', label: 'List chargers', method: 'GET', path: '/api/chargers' },
  { slug: 'ChargerDetails', label: 'Charger details', method: 'GET', path: '/api/chargers/{chargerId}' },
  { slug: 'ChargerState', label: 'Charger state (observations)', method: 'GET', path: '/api/chargers/{chargerId}/state' },
  { slug: 'PauseCharging', label: 'Pause charging (506)', method: 'POST', path: '/api/chargers/{chargerId}/sendCommand/506' },
  { slug: 'ResumeCharging', label: 'Resume charging (507)', method: 'POST', path: '/api/chargers/{chargerId}/sendCommand/507' },
  { slug: 'RestartCharger', label: 'Restart charger (102)', method: 'POST', path: '/api/chargers/{chargerId}/sendCommand/102' },
  { slug: 'Deauthorize', label: 'Deauthorize and stop (10001)', method: 'POST', path: '/api/chargers/{chargerId}/sendCommand/10001' },
  { slug: 'UpdateCharger', label: 'Update charger (max current)', method: 'POST', path: '/api/chargers/{chargerId}/update', body: { maxChargeCurrent: 16 } },
  { slug: 'ListInstallations', label: 'List installations', method: 'GET', path: '/api/installation' },
  { slug: 'InstallationDetails', label: 'Installation details', method: 'GET', path: '/api/installation/{installationId}' },
  { slug: 'InstallationHierarchy', label: 'Installation hierarchy', method: 'GET', path: '/api/installation/{installationId}/hierarchy' },
  { slug: 'UpdateInstallation', label: 'Update installation (available current)', method: 'POST', path: '/api/installation/{installationId}/update', body: { availableCurrent: 16 } },
  { slug: 'SessionDetails', label: 'Session details', method: 'GET', path: '/api/session/{sessionId}' },
  { slug: 'ArchivedSessions', label: 'Archived sessions', method: 'GET', path: '/api/sessions/archived?ChargerId={chargerId}' },
  { slug: 'Constants', label: 'Constants (observation ids, enums)', method: 'GET', path: '/api/constants' },
]

// Templates and ./zaptec-docs/*.md are matched by slug; shout in dev when one is missing.
if (import.meta.env.DEV) {
  const undocumented = ZAPTEC_TEMPLATES.map((t) => t.slug).filter(
    (s) => !DOCUMENTED_ZAPTEC_CALLS.includes(s),
  )
  if (undocumented.length > 0) {
    console.warn(`No zaptec-docs/<slug>.md for: ${undocumented.join(', ')}`)
  }
}

function ZaptecCallPanel({
  chargerId,
  slug,
  setSlug,
}: {
  chargerId: string
  slug: string
  setSlug: (slug: string) => void
}) {
  const fill = (path: string) => path.replace('{chargerId}', chargerId || '{chargerId}')
  const [method, setMethod] = useState<ZaptecMethod>('GET')
  const [path, setPath] = useState(fill(ZAPTEC_TEMPLATES[0].path))
  const [bodyText, setBodyText] = useState('')
  const [parseError, setParseError] = useState<string | null>(null)
  const sendCall = useSendZaptecApiCall()

  function applyTemplate(templateSlug: string) {
    const template = ZAPTEC_TEMPLATES.find((t) => t.slug === templateSlug)
    if (!template) return
    setSlug(template.slug)
    setMethod(template.method)
    setPath(fill(template.path))
    setBodyText(template.body ? JSON.stringify(template.body, null, 2) : '')
    setParseError(null)
  }

  function handleBodyChange(text: string) {
    setBodyText(text)
    if (!text.trim()) {
      setParseError(null)
      return
    }
    try {
      JSON.parse(text)
      setParseError(null)
    } catch (err) {
      setParseError(err instanceof Error ? err.message : 'Invalid JSON')
    }
  }

  async function handleSend() {
    if (!window.confirm(`Send ${method} ${path} to the Zaptec API?`)) return
    sendCall.mutate({ method, path, body: bodyText.trim() ? bodyText : null })
  }

  const canSend = path.startsWith('/api/') && !parseError && !sendCall.isPending
  const result = sendCall.data

  return (
    <Panel
      title="Send Zaptec API call"
      actions={
        <span
          className="flex items-center gap-1.5 text-xs text-amber-400"
          title="Requests go to the Zaptec cloud with your account's token and can pause, resume or reconfigure the charger."
        >
          <AlertTriangle className="h-3.5 w-3.5 shrink-0" />
          Goes straight to the Zaptec cloud — commands affect the real charger
        </span>
      }
    >
      <div className="space-y-3">
        <div className="flex flex-wrap items-end gap-2">
          <label className="min-w-0 flex-1 text-sm">
            <span className="mb-1 block text-[#8892a4]">Template</span>
            <select onChange={(e) => applyTemplate(e.target.value)} value={slug} className={inputClass}>
              {ZAPTEC_TEMPLATES.map((t) => (
                <option key={t.slug} value={t.slug}>
                  {t.label}
                </option>
              ))}
            </select>
          </label>
          <label className="w-28 text-sm">
            <span className="mb-1 block text-[#8892a4]">Method</span>
            <select value={method} onChange={(e) => setMethod(e.target.value as ZaptecMethod)} className={inputClass}>
              {(['GET', 'POST', 'PUT', 'DELETE'] as const).map((m) => (
                <option key={m}>{m}</option>
              ))}
            </select>
          </label>
          <button
            onClick={handleSend}
            disabled={!canSend}
            className="shrink-0 rounded-md bg-blue-600 px-5 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-500 disabled:opacity-50"
          >
            {sendCall.isPending ? 'Sending…' : 'Send'}
          </button>
        </div>

        <label className="block text-sm">
          <span className="mb-1 block text-[#8892a4]">Path (relative to api.zaptec.com)</span>
          <input value={path} onChange={(e) => setPath(e.target.value)} className={cn(inputClass, 'font-mono text-xs')} />
        </label>

        <div className="text-sm">
          <span className="mb-1 block text-[#8892a4]">Body (JSON, optional)</span>
          <div
            className={`overflow-auto rounded-md border bg-[#0f1117] focus-within:border-blue-500 ${
              parseError ? 'border-red-500/60' : 'border-[#2a3042]'
            }`}
          >
            <Editor
              value={bodyText}
              onValueChange={handleBodyChange}
              highlight={highlightJson}
              padding={12}
              tabSize={2}
              insertSpaces
              textareaClassName="outline-none"
              className="min-h-[6rem] font-mono text-xs leading-relaxed text-[#c3cad8]"
            />
          </div>
        </div>
        {parseError && <div className="text-xs text-red-400">{parseError}</div>}

        {(result || sendCall.isError) && (
          <div className="space-y-2">
            <Badge tone={result?.success ? 'success' : 'danger'}>
              {result ? `HTTP ${result.statusCode}` : 'Failed'}
            </Badge>
            {result ? (
              <JsonBlock
                code={result.body ? tryFormatJson(result.body) : '(empty response)'}
                className="max-h-[24rem] overflow-y-auto"
              />
            ) : (
              <pre className="overflow-x-auto rounded-md border border-[#2a3042] bg-[#0f1117] p-3 text-xs text-red-300">
                {sendCall.error instanceof ApiError ? sendCall.error.message : 'Request failed.'}
              </pre>
            )}
          </div>
        )}
      </div>
    </Panel>
  )
}

export function ZaptecDeveloperTab() {
  const { data: chargerSettings } = useChargerSettings()
  // Lifted so the reference column tracks the selected template next to the call panel.
  const [slug, setSlug] = useState(ZAPTEC_TEMPLATES[0].slug)

  return (
    // Same two-column layout as the OCPP page: console left, reference right.
    <div className="flex flex-col gap-4 lg:grid lg:h-full lg:min-h-0 lg:grid-cols-2 lg:gap-4">
      <div className="flex flex-col gap-4 lg:min-h-0">
        <ZaptecCallPanel chargerId={chargerSettings?.zaptecChargerId ?? ''} slug={slug} setSlug={setSlug} />
      </div>

      <Suspense
        fallback={
          <div className="flex items-center gap-2 rounded-lg border border-[#2a3042] bg-[#151a26] p-4 text-xs text-[#8892a4]">
            <Loader2 className="h-3.5 w-3.5 animate-spin" /> Loading reference…
          </div>
        }
      >
        <ZaptecDocPanel slug={slug} />
      </Suspense>
    </div>
  )
}
