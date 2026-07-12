import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

export type BadgeTone = 'neutral' | 'success' | 'warning' | 'danger' | 'info'

const TONE_CLASSES: Record<BadgeTone, string> = {
  neutral: 'bg-[#2a3042] text-[#8892a4]',
  success: 'bg-emerald-500/10 text-emerald-400',
  warning: 'bg-amber-500/10 text-amber-400',
  danger: 'bg-red-500/10 text-red-400',
  info: 'bg-blue-500/10 text-blue-400',
}

interface BadgeProps {
  tone?: BadgeTone
  children: ReactNode
  pulse?: boolean
  className?: string
}

export function Badge({ tone = 'neutral', children, pulse, className }: BadgeProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium',
        TONE_CLASSES[tone],
        className,
      )}
    >
      {pulse && (
        <span
          className={cn(
            'h-1.5 w-1.5 rounded-full animate-pulse',
            tone === 'success' && 'bg-emerald-400',
            tone === 'warning' && 'bg-amber-400',
            tone === 'danger' && 'bg-red-400',
            tone === 'info' && 'bg-blue-400',
            tone === 'neutral' && 'bg-[#8892a4]',
          )}
        />
      )}
      {children}
    </span>
  )
}
