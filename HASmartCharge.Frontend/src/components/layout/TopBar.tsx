import type { ReactNode } from 'react'
import { Bell } from 'lucide-react'

interface TopBarProps {
  title: string
  subtitle?: string
  actions?: ReactNode
}

export function TopBar({ title, subtitle, actions }: TopBarProps) {
  return (
    <header className="flex h-14 shrink-0 items-center justify-between border-b border-[#2a3042] bg-[#1a1f2e] px-6">
      <div>
        <h1 className="text-base font-semibold text-white">{title}</h1>
        {subtitle && <p className="text-xs text-[#8892a4]">{subtitle}</p>}
      </div>
      <div className="flex items-center gap-3">
        <div className="flex items-center gap-1.5 rounded-full bg-emerald-500/10 px-3 py-1 text-xs font-medium text-emerald-400">
          <span className="h-1.5 w-1.5 rounded-full bg-emerald-400 animate-pulse" />
          System Live
        </div>
        {actions}
        <button className="relative rounded-md p-1.5 text-[#8892a4] hover:bg-[#232938] hover:text-white transition-colors">
          <Bell className="h-4 w-4" />
        </button>
      </div>
    </header>
  )
}

