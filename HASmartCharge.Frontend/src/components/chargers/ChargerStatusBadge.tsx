interface ChargerStatusBadgeProps {
  isConnected: boolean
}

export function ChargerStatusBadge({ isConnected }: ChargerStatusBadgeProps) {
  return (
    <div className="flex items-center gap-1.5">
      <span
        className={`h-2 w-2 rounded-full shrink-0 ${isConnected ? 'bg-emerald-400 shadow-[0_0_6px_theme(colors.emerald.400)]' : 'bg-[#4a5568]'
          }`}
      />
      <span
        className={`text-xs font-semibold ${isConnected ? 'text-emerald-400' : 'text-[#8892a4]'
          }`}
      >
        {isConnected ? 'Online' : 'Offline'}
      </span>
    </div>
  )
}
