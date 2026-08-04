import { useState } from 'react'
import { AlertTriangle, ArrowDownToLine, ArrowUpFromLine, Trash2 } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { useSendOcppCall } from '@/hooks/useCharger'
import { useOcppFrameLog } from '@/hooks/useOcppFrameLog'
import { formatDateTime } from '@/lib/utils'
import { ApiError } from '@/api/client'

const inputClass =
  'w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500'

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

function OcppCallPanel() {
  const [action, setAction] = useState(CALL_TEMPLATES[0].action)
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
    <div className="space-y-4">
      <h3 className="text-sm font-semibold text-white">Send OCPP call</h3>
      <p className="flex items-start gap-2 text-xs text-amber-400">
        <AlertTriangle className="h-4 w-4 shrink-0 mt-0.5" />
        These calls go straight to the charger over the active OCPP session and can interrupt an
        active charge session. Use with care.
      </p>

      <label className="text-sm block">
        <span className="text-[#8892a4] block mb-1">Template</span>
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

      <label className="text-sm block">
        <span className="text-[#8892a4] block mb-1">Action</span>
        <input value={action} onChange={(e) => setAction(e.target.value)} className={inputClass} />
      </label>

      <label className="text-sm block">
        <span className="text-[#8892a4] block mb-1">Payload (JSON)</span>
        <textarea
          value={payloadText}
          onChange={(e) => handlePayloadChange(e.target.value)}
          rows={10}
          className={`${inputClass} font-mono text-xs`}
        />
      </label>
      {parseError && <div className="text-sm text-red-400">{parseError}</div>}

      <button
        onClick={handleSend}
        disabled={!canSend}
        className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-500 disabled:opacity-50"
      >
        {sendCall.isPending ? 'Sending…' : 'Send'}
      </button>

      {(sendCall.data || sendCall.isError) && (
        <div className="space-y-2">
          <Badge tone={sendCall.data?.success ? 'success' : 'danger'}>
            {sendCall.data?.success ? 'Success' : 'Failed'}
          </Badge>
          <pre className="max-w-full overflow-x-auto rounded-md border border-[#2a3042] bg-[#0f1117] p-3 text-xs text-[#c3cad8]">
            {sendCall.data
              ? JSON.stringify(sendCall.data, null, 2)
              : sendCall.error instanceof ApiError
                ? sendCall.error.message
                : 'Request failed.'}
          </pre>
        </div>
      )}
    </div>
  )
}

const CONNECTION_BADGE: Record<ReturnType<typeof useOcppFrameLog>['state'], { tone: 'success' | 'warning' | 'danger' | 'info'; label: string }> = {
  connecting: { tone: 'info', label: 'Connecting…' },
  connected: { tone: 'success', label: 'Connected' },
  reconnecting: { tone: 'warning', label: 'Reconnecting…' },
  disconnected: { tone: 'danger', label: 'Disconnected' },
}

function OcppLogPanel() {
  const { frames, state, clear } = useOcppFrameLog()
  const badge = CONNECTION_BADGE[state]

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-white">Live OCPP log</h3>
        <div className="flex items-center gap-2">
          <Badge tone={badge.tone} pulse={state === 'connected'}>
            {badge.label}
          </Badge>
          <button
            onClick={clear}
            className="flex items-center gap-1.5 rounded-md border border-[#2a3042] bg-[#232938] px-3 py-1.5 text-xs font-medium text-white transition-colors hover:bg-[#2a3042]"
          >
            <Trash2 className="h-3.5 w-3.5" /> Clear
          </button>
        </div>
      </div>

      {frames.length === 0 ? (
        <p className="text-sm text-[#8892a4] py-6">
          No frames yet. Frames appear here as the charger talks to the backend — expect at least
          a Heartbeat roughly every heartbeat interval once connected.
        </p>
      ) : (
        <div className="space-y-1.5 max-h-[32rem] overflow-y-auto">
          {frames.map((f, i) => (
            <div
              key={`${f.timestampUtc}-${i}`}
              className="flex items-start gap-2 rounded-md border border-[#2a3042] bg-[#0f1117] px-2.5 py-2 text-xs"
            >
              <Badge tone={f.direction === 'in' ? 'info' : 'neutral'} className="shrink-0">
                {f.direction === 'in' ? (
                  <ArrowDownToLine className="h-3 w-3" />
                ) : (
                  <ArrowUpFromLine className="h-3 w-3" />
                )}
                {f.direction === 'in' ? 'IN' : 'OUT'}
              </Badge>
              <span className="shrink-0 text-[#8892a4]">{formatDateTime(f.timestampUtc)}</span>
              <span className="min-w-0 flex-1 overflow-x-auto whitespace-pre font-mono text-[#c3cad8]">
                {f.frame}
              </span>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

export function DeveloperTab() {
  return (
    <div className="max-w-3xl space-y-8">
      <OcppCallPanel />
      <div className="border-t border-[#2a3042] pt-6">
        <OcppLogPanel />
      </div>
    </div>
  )
}
