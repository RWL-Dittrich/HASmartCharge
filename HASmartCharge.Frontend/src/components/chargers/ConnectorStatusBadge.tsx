import type { ConnectorStatus } from '@/types/charger'

interface ConnectorStatusBadgeProps {
  status: ConnectorStatus | null
}

const STATUS_CONFIG: Record<
  ConnectorStatus,
  { dot: string; text: string; label: string }
> = {
  Available: { dot: 'bg-emerald-400', text: 'text-emerald-400', label: 'Available' },
  Preparing: { dot: 'bg-yellow-400', text: 'text-yellow-400', label: 'Preparing' },
  Charging: { dot: 'bg-blue-400', text: 'text-blue-400', label: 'Charging' },
  SuspendedEVSE: { dot: 'bg-orange-400', text: 'text-orange-400', label: 'Suspended EVSE' },
  SuspendedEV: { dot: 'bg-orange-300', text: 'text-orange-300', label: 'Suspended EV' },
  Finishing: { dot: 'bg-yellow-400', text: 'text-yellow-400', label: 'Finishing' },
  Reserved: { dot: 'bg-purple-400', text: 'text-purple-400', label: 'Reserved' },
  Unavailable: { dot: 'bg-[#4a5568]', text: 'text-[#8892a4]', label: 'Unavailable' },
  Faulted: { dot: 'bg-red-400', text: 'text-red-400', label: 'Faulted' },
  Unknown: { dot: 'bg-[#4a5568]', text: 'text-[#8892a4]', label: 'Unknown' },
}

export function ConnectorStatusBadge({ status }: ConnectorStatusBadgeProps) {
  const config = STATUS_CONFIG[status ?? 'Unknown'] ?? STATUS_CONFIG.Unknown
  return (
    <div className="flex items-center gap-1.5">
      <span className={`h-2 w-2 rounded-full shrink-0 ${config.dot}`} />
      <span className={`text-xs font-semibold ${config.text}`}>{config.label}</span>
    </div>
  )
}
