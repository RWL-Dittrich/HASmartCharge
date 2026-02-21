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
    <div className={cn('rounded-lg bg-[#1a1f2e] border border-[#2a3042] p-4', className)}>
      <div className="text-xs text-[#8892a4] uppercase tracking-wide mb-1">{title}</div>
      <div className="text-2xl font-bold text-white">{value}</div>
      {change && (
        <div
          className={cn(
            'mt-1 text-xs font-medium',
            changePositive ? 'text-emerald-400' : 'text-red-400',
          )}
        >
          {changePositive ? '▲' : '▼'} {change}
        </div>
      )}
    </div>
  )
}

