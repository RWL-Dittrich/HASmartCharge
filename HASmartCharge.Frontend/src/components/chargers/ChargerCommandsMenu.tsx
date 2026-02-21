import { useState, useEffect, useRef } from 'react'
import { X, MoreVertical, RefreshCw, Trash2, Radio, AlertTriangle } from 'lucide-react'
import {
  useClearCache,
  useResetCharger,
  useTriggerMessage,
} from '@/hooks/useChargerCommands'
import type { ResetType } from '@/types/charger'

// ---------------------------------------------------------------------------
// Trigger message options (OCPP 1.6)
// ---------------------------------------------------------------------------

const TRIGGER_OPTIONS: string[] = [
  'BootNotification',
  'DiagnosticsStatusNotification',
  'FirmwareStatusNotification',
  'Heartbeat',
  'MeterValues',
  'StatusNotification',
]

interface ChargerCommandsMenuProps {
  chargerId: string
  isConnected: boolean
}

export function ChargerCommandsMenu({ chargerId, isConnected }: ChargerCommandsMenuProps) {
  const [open, setOpen] = useState(false)
  const [feedback, setFeedback] = useState<string | null>(null)
  const menuRef = useRef<HTMLDivElement>(null)

  const reset = useResetCharger(chargerId)
  const clear = useClearCache(chargerId)
  const trigger = useTriggerMessage(chargerId)

  // Close on outside click
  useEffect(() => {
    if (!open) return
    function handler(e: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [open])

  // Clear feedback after 3 s
  useEffect(() => {
    if (!feedback) return
    const id = setTimeout(() => setFeedback(null), 3000)
    return () => clearTimeout(id)
  }, [feedback])

  function handleReset(type: ResetType) {
    setOpen(false)
    reset.mutate(type, {
      onSuccess: () => setFeedback(`Reset (${type}) dispatched`),
      onError: (err) => setFeedback((err as Error).message),
    })
  }

  function handleClearCache() {
    setOpen(false)
    clear.mutate(undefined, {
      onSuccess: () => setFeedback('ClearCache dispatched'),
      onError: (err) => setFeedback((err as Error).message),
    })
  }

  function handleTrigger(requestedMessage: string) {
    setOpen(false)
    trigger.mutate({ requestedMessage }, {
      onSuccess: () => setFeedback(`TriggerMessage (${requestedMessage}) dispatched`),
      onError: (err) => setFeedback((err as Error).message),
    })
  }

  const disabled = !isConnected || reset.isPending || clear.isPending || trigger.isPending

  return (
    <div className="relative" ref={menuRef}>
      <button
        onClick={() => setOpen((v) => !v)}
        disabled={disabled}
        title={!isConnected ? 'Charger is offline' : 'Commands'}
        className="rounded-md p-1.5 text-[#8892a4] hover:bg-[#232938] hover:text-white transition-colors disabled:opacity-40"
      >
        <MoreVertical className="h-4 w-4" />
      </button>

      {/* Feedback pill */}
      {feedback && (
        <div className="absolute right-0 bottom-8 z-50 whitespace-nowrap rounded-md bg-[#232938] border border-[#2a3042] px-3 py-1.5 text-xs text-white shadow-lg">
          {feedback}
        </div>
      )}

      {/* Dropdown */}
      {open && (
        <div className="absolute right-0 top-8 z-50 w-52 rounded-lg bg-[#1e2435] border border-[#2a3042] shadow-xl py-1">
          {/* Reset section */}
          <div className="px-3 py-1.5 text-[10px] font-semibold uppercase tracking-wider text-[#8892a4]">
            Reset
          </div>
          <button
            onClick={() => handleReset('Soft')}
            className="flex w-full items-center gap-2.5 px-3 py-2 text-xs text-white hover:bg-[#2a3042] transition-colors"
          >
            <RefreshCw className="h-3.5 w-3.5 text-[#8892a4]" />
            Soft Reset
          </button>
          <button
            onClick={() => handleReset('Hard')}
            className="flex w-full items-center gap-2.5 px-3 py-2 text-xs text-red-400 hover:bg-[#2a3042] transition-colors"
          >
            <AlertTriangle className="h-3.5 w-3.5" />
            Hard Reset
          </button>

          <div className="my-1 border-t border-[#2a3042]" />

          {/* Cache */}
          <div className="px-3 py-1.5 text-[10px] font-semibold uppercase tracking-wider text-[#8892a4]">
            Maintenance
          </div>
          <button
            onClick={handleClearCache}
            className="flex w-full items-center gap-2.5 px-3 py-2 text-xs text-white hover:bg-[#2a3042] transition-colors"
          >
            <Trash2 className="h-3.5 w-3.5 text-[#8892a4]" />
            Clear Cache
          </button>

          <div className="my-1 border-t border-[#2a3042]" />

          {/* Trigger messages */}
          <div className="px-3 py-1.5 text-[10px] font-semibold uppercase tracking-wider text-[#8892a4]">
            Trigger Message
          </div>
          {TRIGGER_OPTIONS.map((msg) => (
            <button
              key={msg}
              onClick={() => handleTrigger(msg)}
              className="flex w-full items-center gap-2.5 px-3 py-2 text-xs text-white hover:bg-[#2a3042] transition-colors"
            >
              <Radio className="h-3.5 w-3.5 text-[#8892a4]" />
              {msg}
            </button>
          ))}
        </div>
      )}

      {/* Dismiss overlay for keyboard a11y */}
      {open && (
        <div
          className="fixed inset-0 z-40"
          aria-hidden
          onClick={() => setOpen(false)}
        />
      )}
    </div>
  )
}

// Re-export close button for Drawer use
export { X }
