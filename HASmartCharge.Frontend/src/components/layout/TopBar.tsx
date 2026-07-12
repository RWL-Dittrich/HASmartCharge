import type { ReactNode } from 'react'
import { Bell, Menu } from 'lucide-react'
import { useSidebar } from './LayoutContext'

interface TopBarProps {
  title: string
  subtitle?: string
  actions?: ReactNode
}

export function TopBar({ title, subtitle, actions }: TopBarProps) {
  const { open } = useSidebar()

  return (
    <header className="flex h-14 shrink-0 items-center justify-between gap-2 border-b border-[#2a3042] bg-[#1a1f2e] px-3 sm:px-6">
      <div className="flex min-w-0 items-center gap-2">
        <button
          onClick={open}
          className="-ml-1 rounded-md p-1.5 text-[#8892a4] transition-colors hover:bg-[#232938] hover:text-white md:hidden"
          aria-label="Open menu"
        >
          <Menu className="h-5 w-5" />
        </button>
        <div className="min-w-0">
          <h1 className="truncate text-sm font-semibold text-white sm:text-base">{title}</h1>
          {subtitle && <p className="hidden truncate text-xs text-[#8892a4] sm:block">{subtitle}</p>}
        </div>
      </div>
      <div className="flex shrink-0 items-center gap-2 sm:gap-3">
        <div className="flex items-center gap-1.5 rounded-full bg-emerald-500/10 px-2 py-1 text-xs font-medium text-emerald-400 sm:px-3">
          <span className="h-1.5 w-1.5 rounded-full bg-emerald-400 animate-pulse" />
          <span className="hidden sm:inline">System Live</span>
        </div>
        {actions}
        <button className="relative rounded-md p-1.5 text-[#8892a4] transition-colors hover:bg-[#232938] hover:text-white">
          <Bell className="h-4 w-4" />
        </button>
      </div>
    </header>
  )
}
