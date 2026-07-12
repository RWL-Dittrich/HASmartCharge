import { cn } from '@/lib/utils'

interface StatCardProps {
  title: string
  value: string | number
  change?: string
  changePositive?: boolean
  className?: string
}

export function StatCard({ title, value, change, changePositive, className }: StatCardProps) {
  return (
    <div className={cn('min-w-0 rounded-lg bg-[#1a1f2e] border border-[#2a3042] p-3 sm:p-4', className)}>
      <div className="text-[10px] sm:text-xs text-[#8892a4] uppercase tracking-wide mb-1">{title}</div>
      <div className="text-lg sm:text-2xl font-bold text-white [overflow-wrap:anywhere]">{value}</div>
      {change && (
        <div
          className={cn(
            'mt-1 text-xs font-medium [overflow-wrap:anywhere]',
            changePositive ? 'text-emerald-400' : 'text-red-400',
          )}
        >
          {changePositive ? '▲' : '▼'} {change}
        </div>
      )}
    </div>
  )
}

