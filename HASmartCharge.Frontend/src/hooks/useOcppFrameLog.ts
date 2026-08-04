import { useEffect, useState } from 'react'
import { HubConnectionBuilder, type HubConnection } from '@microsoft/signalr'
import type { OcppFrame } from '@/types/charger'

const MAX_FRAMES = 50

export type OcppFrameLogState = 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

// Resolve against document.baseURI the same way api/client.ts's resolveUrl does, so the hub
// path respects the HA ingress prefix when present.
function resolveHubUrl(): string {
  return new URL('api/hubs/ocpp-log', document.baseURI).toString()
}

/** Live-tails the last 50 OCPP frames (newest first) over the ocpp-log SignalR hub. */
export function useOcppFrameLog() {
  const [frames, setFrames] = useState<OcppFrame[]>([])
  const [state, setState] = useState<OcppFrameLogState>('connecting')

  useEffect(() => {
    let cancelled = false
    const connection: HubConnection = new HubConnectionBuilder()
      .withUrl(resolveHubUrl())
      .withAutomaticReconnect()
      .build()

    connection.on('frames', (seed: OcppFrame[]) => {
      if (cancelled) return
      setFrames([...seed].reverse().slice(0, MAX_FRAMES))
    })
    connection.on('frame', (next: OcppFrame) => {
      if (cancelled) return
      setFrames((prev) => [next, ...prev].slice(0, MAX_FRAMES))
    })
    connection.onreconnecting(() => !cancelled && setState('reconnecting'))
    connection.onreconnected(() => !cancelled && setState('connected'))
    connection.onclose(() => !cancelled && setState('disconnected'))

    setState('connecting')
    connection
      .start()
      .then(() => {
        if (!cancelled) setState('connected')
      })
      .catch(() => {
        if (!cancelled) setState('disconnected')
      })

    return () => {
      cancelled = true
      connection.stop()
    }
  }, [])

  function clear() {
    setFrames([])
  }

  return { frames, state, clear }
}

