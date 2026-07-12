import { useState } from 'react'
import { Loader2, Wifi, WifiOff } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { useDisconnectHa, useHaStatus } from '@/hooks/useHa'
import { startHaConnect } from '@/api/haApi'
import { formatDateTime } from '@/lib/utils'

export function HomeAssistantTab() {
  const { data: status, isLoading } = useHaStatus()
  const disconnect = useDisconnectHa()
  const [baseUrl, setBaseUrl] = useState('http://homeassistant.local:8123')

  if (isLoading) {
    return (
      <div className="flex items-center gap-2 text-sm text-[#8892a4] py-8">
        <Loader2 className="h-4 w-4 animate-spin" /> Loading…
      </div>
    )
  }

  return (
    <div className="max-w-xl space-y-5">
      <div className="flex items-center justify-between rounded-lg border border-[#2a3042] bg-[#0f1117] p-4">
        <div>
          <div className="flex items-center gap-2">
            <span className="text-sm font-medium text-white">Connection status</span>
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
          {status?.connected && (
            <div className="mt-1 text-xs text-[#8892a4] space-y-0.5">
              <div>{status.baseUrl}</div>
              {status.tokenExpiresAt && <div>Token expires {formatDateTime(status.tokenExpiresAt)}</div>}
            </div>
          )}
        </div>
      </div>

      {status?.connected ? (
        <button
          onClick={() => disconnect.mutate()}
          disabled={disconnect.isPending}
          className="rounded-md border border-red-500/30 bg-red-500/10 px-4 py-2 text-sm font-medium text-red-400 transition-colors hover:bg-red-500/20 disabled:opacity-50"
        >
          {disconnect.isPending ? 'Disconnecting…' : 'Disconnect'}
        </button>
      ) : (
        <div className="space-y-3">
          <label className="text-sm block">
            <span className="text-[#8892a4] block mb-1">Home Assistant base URL</span>
            <input
              value={baseUrl}
              onChange={(e) => setBaseUrl(e.target.value)}
              placeholder="http://homeassistant.local:8123"
              className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
            />
          </label>
          <button
            onClick={() => startHaConnect(baseUrl)}
            disabled={!baseUrl.trim()}
            className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-500 disabled:opacity-50"
          >
            Connect
          </button>
          <p className="text-xs text-[#8892a4]">
            You'll be redirected to Home Assistant to authorize this app, then back here.
          </p>
        </div>
      )}
    </div>
  )
}
