import { lazy, Suspense, useState, type ReactNode } from 'react'
import Editor from 'react-simple-code-editor'
import {
  AlertTriangle,
  ArrowDownToLine,
  ArrowUpFromLine,
  ChevronRight,
  Loader2,
  Trash2,
} from 'lucide-react'
import { Badge, type BadgeTone } from '@/components/ui/Badge'
import { useSendOcppCall } from '@/hooks/useCharger'
import { useOcppFrameLog } from '@/hooks/useOcppFrameLog'
import { cn, formatDateTime } from '@/lib/utils'
import { highlightJson, tryFormatJson } from '@/lib/highlightJson'
import { ApiError } from '@/api/client'
import type { OcppFrame } from '@/types/charger'
import { DOCUMENTED_ACTIONS } from './ocppDocs'

// Lazy so react-markdown (~165 kB) stays out of the main bundle — it is only ever needed here.
const OcppDocPanel = lazy(() =>
  import('./OcppDocPanel').then((m) => ({ default: m.OcppDocPanel })),
)

const inputClass =
  'w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500'

/**
 * Card wrapper: title bar with optional right-side controls, then the body. `fill` makes the card
 * take the height its grid cell offers and scroll its body instead of growing the page.
 */
function Panel({
  title,
  actions,
  fill,
  children,
}: {
  title: string
  actions?: ReactNode
  fill?: boolean
  children: ReactNode
}) {
  return (
    <section
      className={cn(
        'rounded-lg border border-[#2a3042] bg-[#151a26] shadow-lg shadow-black/20',
        fill && 'flex min-h-0 flex-col lg:flex-1 lg:overflow-hidden',
      )}
    >
      <header className="flex shrink-0 flex-wrap items-center justify-between gap-2 border-b border-[#2a3042] px-4 py-3">
        <h3 className="text-sm font-semibold text-white">{title}</h3>
        {actions && <div className="flex items-center gap-2">{actions}</div>}
      </header>
      <div className={cn('p-4', fill && 'min-h-0 flex-1 lg:overflow-y-auto')}>{children}</div>
    </section>
  )
}

interface CallTemplate {
  action: string
  // A thunk for payloads carrying a timestamp: this array is evaluated once at module load, so a
  // literal `new Date()` would freeze at page load and go stale in a long-lived tab.
  payload: object | (() => object)
}

// Example payloads for the OCPP 1.6J core + smart-charging call set. Field names are the exact
// on-wire camelCase names from the spec.
const CALL_TEMPLATES: CallTemplate[] = [
  { action: 'Reset', payload: { type: 'Soft' } },
  { action: 'RemoteStartTransaction', payload: { connectorId: 1, idTag: 'DEV0000000001' } },
  { action: 'RemoteStopTransaction', payload: { transactionId: 1 } },
  { action: 'TriggerMessage', payload: { requestedMessage: 'BootNotification' } },
  { action: 'GetConfiguration', payload: { key: ['HeartbeatInterval'] } },
  { action: 'ChangeConfiguration', payload: { key: 'HeartbeatInterval', value: '60' } },
  { action: 'ChangeAvailability', payload: { connectorId: 1, type: 'Operative' } },
  { action: 'UnlockConnector', payload: { connectorId: 1 } },
  {
    action: 'SetChargingProfile',
    payload: {
      connectorId: 1,
      csChargingProfiles: {
        chargingProfileId: 1,
        stackLevel: 0,
        chargingProfilePurpose: 'TxDefaultProfile',
        chargingProfileKind: 'Relative',
        chargingSchedule: {
          chargingRateUnit: 'A',
          chargingSchedulePeriod: [{ startPeriod: 0, limit: 16, numberPhases: 3 }],
        },
      },
    },
  },
  { action: 'ClearChargingProfile', payload: { connectorId: 1 } },
  { action: 'GetCompositeSchedule', payload: { connectorId: 1, duration: 3600 } },
  { action: 'GetLocalListVersion', payload: {} },
  {
    action: 'SendLocalList',
    payload: {
      listVersion: 1,
      updateType: 'Full',
      localAuthorizationList: [{ idTag: 'DEV0000000001', idTagInfo: { status: 'Accepted' } }],
    },
  },
  {
    action: 'ReserveNow',
    payload: () => ({
      connectorId: 1,
      expiryDate: new Date(Date.now() + 3600_000).toISOString(),
      idTag: 'DEV0000000001',
      reservationId: 1,
    }),
  },
  { action: 'CancelReservation', payload: { reservationId: 1 } },
  { action: 'DataTransfer', payload: { vendorId: 'com.example', messageId: 'Ping', data: '' } },
  {
    action: 'GetDiagnostics',
    payload: { location: 'ftp://user:pass@example.com/diagnostics/' },
  },
  {
    action: 'UpdateFirmware',
    payload: () => ({
      location: 'ftp://user:pass@example.com/firmware.bin',
      retrieveDate: new Date().toISOString(),
    }),
  },
  { action: 'ClearCache', payload: {} },
]

function formatPayload(payload: CallTemplate['payload']): string {
  return JSON.stringify(typeof payload === 'function' ? payload() : payload, null, 2)
}

// Templates and ./ocpp-docs/*.md are matched by action name, so a rename on one side silently
// drops the reference panel. Shout about it in dev rather than shipping a blank panel.
if (import.meta.env.DEV) {
  const undocumented = CALL_TEMPLATES.map((t) => t.action).filter(
    (a) => !DOCUMENTED_ACTIONS.includes(a),
  )
  if (undocumented.length > 0) {
    console.warn(`No ocpp-docs/<Action>.md for: ${undocumented.join(', ')}`)
  }
}

/** Prism-highlighted, read-only JSON block. */
function JsonBlock({ code, className }: { code: string; className?: string }) {
  return (
    <pre
      className={cn(
        'overflow-x-auto rounded-md border border-[#2a3042] bg-[#0f1117] p-3 font-mono text-xs leading-relaxed',
        className,
      )}
    >
      <code dangerouslySetInnerHTML={{ __html: highlightJson(code) }} />
    </pre>
  )
}

function OcppCallPanel({
  action,
  setAction,
}: {
  action: string
  setAction: (action: string) => void
}) {
  const [payloadText, setPayloadText] = useState(formatPayload(CALL_TEMPLATES[0].payload))
  const [parseError, setParseError] = useState<string | null>(null)
  const sendCall = useSendOcppCall()

  function applyTemplate(templateAction: string) {
    const template = CALL_TEMPLATES.find((t) => t.action === templateAction)
    if (!template) return
    setAction(template.action)
    setPayloadText(formatPayload(template.payload))
    setParseError(null)
  }

  function validate(text: string): unknown | undefined {
    if (!text.trim()) {
      setParseError(null)
      return {}
    }
    try {
      const parsed = JSON.parse(text)
      setParseError(null)
      return parsed
    } catch (err) {
      setParseError(err instanceof Error ? err.message : 'Invalid JSON')
      return undefined
    }
  }

  function handlePayloadChange(text: string) {
    setPayloadText(text)
    validate(text)
  }

  async function handleSend() {
    const payload = validate(payloadText)
    if (payload === undefined) return
    if (!window.confirm(`Send "${action}" to the charger?`)) return
    sendCall.mutate({ action, payload })
  }

  const canSend = action.trim().length > 0 && !parseError && !sendCall.isPending

  return (
    <Panel
      title="Send OCPP call"
      actions={
        <span
          className="flex items-center gap-1.5 text-xs text-amber-400"
          title="These calls go straight to the charger over the active OCPP session and can interrupt an active charge session."
        >
          <AlertTriangle className="h-3.5 w-3.5 shrink-0" />
          Goes straight to the charger — can interrupt a charge session
        </span>
      }
    >
      <div className="space-y-3">
        {/* Template, Action and Send share one row so the three controls cost one row, not three. */}
        <div className="flex flex-wrap items-end gap-2">
          <label className="min-w-0 flex-1 text-sm">
            <span className="mb-1 block text-[#8892a4]">Template</span>
            <select
              onChange={(e) => applyTemplate(e.target.value)}
              defaultValue={CALL_TEMPLATES[0].action}
              className={inputClass}
            >
              {CALL_TEMPLATES.map((t) => (
                <option key={t.action} value={t.action}>
                  {t.action}
                </option>
              ))}
            </select>
          </label>

          <label className="min-w-0 flex-1 text-sm">
            <span className="mb-1 block text-[#8892a4]">Action</span>
            <input
              value={action}
              onChange={(e) => setAction(e.target.value)}
              className={inputClass}
            />
          </label>
          <button
            onClick={handleSend}
            disabled={!canSend}
            className="shrink-0 rounded-md bg-blue-600 px-5 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-500 disabled:opacity-50"
          >
            {sendCall.isPending ? 'Sending…' : 'Send'}
          </button>
        </div>

        <div className="text-sm">
          <div className="mb-1 flex items-end justify-between gap-2">
            <span className="text-[#8892a4]">Payload (JSON)</span>
            <button
              type="button"
              onClick={() => handlePayloadChange(tryFormatJson(payloadText))}
              disabled={!!parseError}
              className="text-xs text-blue-400 hover:underline disabled:opacity-40 disabled:hover:no-underline"
            >
              Format
            </button>
          </div>
          {/* Textareas cannot be syntax-highlighted, so this is a transparent textarea layered
              over a Prism-highlighted <pre> — what react-simple-code-editor exists to do. */}
          <div
            className={`overflow-auto rounded-md border bg-[#0f1117] focus-within:border-blue-500 ${
              parseError ? 'border-red-500/60' : 'border-[#2a3042]'
            }`}
          >
            <Editor
              value={payloadText}
              onValueChange={handlePayloadChange}
              highlight={highlightJson}
              padding={12}
              tabSize={2}
              insertSpaces
              textareaClassName="outline-none"
              className="min-h-[9rem] font-mono text-xs leading-relaxed text-[#c3cad8]"
            />
          </div>
        </div>
        {parseError && <div className="text-xs text-red-400">{parseError}</div>}

        {(sendCall.data || sendCall.isError) && (
          <div className="space-y-2">
            <Badge tone={sendCall.data?.success ? 'success' : 'danger'}>
              {sendCall.data?.success ? 'Success' : 'Failed'}
            </Badge>
            {sendCall.data ? (
              <JsonBlock code={JSON.stringify(sendCall.data, null, 2)} />
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

const CONNECTION_BADGE: Record<
  ReturnType<typeof useOcppFrameLog>['state'],
  { tone: BadgeTone; label: string }
> = {
  connecting: { tone: 'info', label: 'Connecting…' },
  connected: { tone: 'success', label: 'Connected' },
  reconnecting: { tone: 'warning', label: 'Reconnecting…' },
  disconnected: { tone: 'danger', label: 'Disconnected' },
}

// OCPP 1.6J wire envelope: [messageTypeId, uniqueId, ...]. CALL adds the action then the payload,
// CALLRESULT adds the payload, CALLERROR adds errorCode, errorDescription, errorDetails.
const MESSAGE_TYPES: Record<number, { label: string; tone: BadgeTone }> = {
  2: { label: 'CALL', tone: 'info' },
  3: { label: 'RESULT', tone: 'success' },
  4: { label: 'ERROR', tone: 'danger' },
}

/** Envelope summary for the frame header, plus the frame re-serialized with indentation. */
function describeFrame(frame: string) {
  try {
    const parsed = JSON.parse(frame)
    if (!Array.isArray(parsed)) return { pretty: JSON.stringify(parsed, null, 2) }

    const [messageType, uniqueId, third] = parsed as [number, string, unknown]
    const type = MESSAGE_TYPES[messageType]
    return {
      pretty: JSON.stringify(parsed, null, 2),
      typeLabel: type?.label ?? `TYPE ${messageType}`,
      typeTone: type?.tone ?? 'neutral',
      // The third element is the action on a CALL and the error code on a CALLERROR.
      subject: messageType === 2 || messageType === 4 ? String(third ?? '') : undefined,
      uniqueId: String(uniqueId ?? ''),
    }
  } catch {
    // A frame the charger sent that isn't valid JSON is exactly what you want to see verbatim.
    return { pretty: frame }
  }
}

function FrameRow({ frame }: { frame: OcppFrame }) {
  const { pretty, typeLabel, typeTone, subject, uniqueId } = describeFrame(frame.frame)
  const inbound = frame.direction === 'in'

  return (
    // Collapsed by default: 50 pretty-printed frames expanded at once is what made this page a
    // wall of JSON. <details> keeps the scan line and puts the body one click away. No border or
    // card of its own — the list divider carries the separation, so nesting stops at the panel.
    <details className="group">
      <summary className="flex cursor-pointer flex-wrap items-center gap-2 px-2 py-2 text-xs hover:bg-[#1b2130] marker:content-none [&::-webkit-details-marker]:hidden">
        <ChevronRight className="h-3 w-3 shrink-0 text-[#8892a4] transition-transform group-open:rotate-90" />
        <Badge tone={inbound ? 'info' : 'neutral'} className="shrink-0">
          {inbound ? <ArrowDownToLine className="h-3 w-3" /> : <ArrowUpFromLine className="h-3 w-3" />}
          {inbound ? 'IN' : 'OUT'}
        </Badge>
        {typeLabel && (
          <Badge tone={typeTone} className="shrink-0">
            {typeLabel}
          </Badge>
        )}
        {subject && <span className="font-medium text-white">{subject}</span>}
        <span className="ml-auto flex items-center gap-2 text-[#8892a4]">
          {uniqueId && <span className="truncate font-mono">#{uniqueId.slice(0, 8)}</span>}
          {formatDateTime(frame.timestampUtc)}
        </span>
      </summary>
      <JsonBlock code={pretty} className="mb-2 rounded-none border-0 border-l-2 border-[#2a3042]" />
    </details>
  )
}

function OcppLogPanel() {
  const { frames, state, clear } = useOcppFrameLog()
  const badge = CONNECTION_BADGE[state]

  return (
    <Panel
      title="Live OCPP log"
      fill
      actions={
        <>
          <span className="text-xs text-[#8892a4]">{frames.length} frames</span>
          <Badge tone={badge.tone} pulse={state === 'connected'}>
            {badge.label}
          </Badge>
          <button
            onClick={clear}
            className="flex items-center gap-1.5 rounded-md border border-[#2a3042] bg-[#232938] px-3 py-1.5 text-xs font-medium text-white transition-colors hover:bg-[#2a3042]"
          >
            <Trash2 className="h-3.5 w-3.5" /> Clear
          </button>
        </>
      }
    >
      {frames.length === 0 ? (
        <p className="py-6 text-sm text-[#8892a4]">
          No frames yet. Frames appear here as the charger talks to the backend — expect at least a
          Heartbeat roughly every heartbeat interval once connected.
        </p>
      ) : (
        <div className="max-h-[30rem] divide-y divide-[#232a3a] overflow-y-auto lg:max-h-none lg:overflow-visible">
          {frames.map((f, i) => (
            <FrameRow key={`${f.timestampUtc}-${i}`} frame={f} />
          ))}
        </div>
      )}
    </Panel>
  )
}

export function DeveloperTab() {
  // Lifted here because the reference column renders next to the call panel, not inside it.
  const [action, setAction] = useState(CALL_TEMPLATES[0].action)

  return (
    // Two full-height columns on wide screens (call + log left, reference right), plain stack below.
    <div className="flex flex-col gap-4 lg:grid lg:h-full lg:min-h-0 lg:grid-cols-2 lg:gap-4">
      <div className="flex flex-col gap-4 lg:min-h-0">
        <OcppCallPanel action={action} setAction={setAction} />
        <OcppLogPanel />
      </div>

      <Suspense
        fallback={
          <div className="flex items-center gap-2 rounded-lg border border-[#2a3042] bg-[#151a26] p-4 text-xs text-[#8892a4]">
            <Loader2 className="h-3.5 w-3.5 animate-spin" /> Loading reference…
          </div>
        }
      >
        <OcppDocPanel action={action} />
      </Suspense>
    </div>
  )
}
