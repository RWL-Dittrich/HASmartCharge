import { NavLink } from 'react-router'
import {
  LayoutDashboard,
  CalendarClock,
  History,
  Settings,
  BatteryCharging,
  X,
} from 'lucide-react'
import { cn } from '@/lib/utils'

const navItems = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard },
  { to: '/schedule', label: 'Schedule', icon: CalendarClock },
  { to: '/history', label: 'History', icon: History },
  { to: '/settings', label: 'Settings', icon: Settings },
]

interface SidebarProps {
  open: boolean
  onClose: () => void
}

export function Sidebar({ open, onClose }: SidebarProps) {
  return (
    <>
      {/* Backdrop — mobile only, when the drawer is open */}
      {open && (
        <div
          className="fixed inset-0 z-30 bg-black/60 md:hidden"
          onClick={onClose}
          aria-hidden
        />
      )}

      <aside
        className={cn(
          'fixed inset-y-0 left-0 z-40 flex h-full w-64 max-w-[80%] flex-col border-r border-[#2a3042] bg-[#1a1f2e] transition-transform duration-200 ease-out',
          'md:static md:z-auto md:w-56 md:max-w-none md:translate-x-0',
          open ? 'translate-x-0' : '-translate-x-full',
        )}
      >
        {/* Logo */}
        <div className="flex items-center gap-3 px-4 py-5 border-b border-[#2a3042]">
          <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-md bg-blue-600">
            <BatteryCharging className="h-4 w-4 text-white" />
          </div>
          <div className="min-w-0 flex-1">
            <div className="truncate text-sm font-semibold text-white">HA Smart Charge</div>
            <div className="truncate text-xs text-[#8892a4]">OCPP Admin Console</div>
          </div>
          <button
            onClick={onClose}
            className="rounded-md p-1 text-[#8892a4] transition-colors hover:bg-[#232938] hover:text-white md:hidden"
            aria-label="Close menu"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Nav */}
        <nav className="flex-1 px-2 py-3 space-y-0.5">
          {navItems.map(({ to, label, icon: Icon }) => (
            <NavLink
              key={to}
              to={to}
              end={to === '/'}
              onClick={onClose}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-3 rounded-md px-3 py-2 text-sm transition-colors',
                  isActive
                    ? 'bg-blue-600/20 text-blue-400 font-medium'
                    : 'text-[#8892a4] hover:bg-[#232938] hover:text-white',
                )
              }
            >
              <Icon className="h-4 w-4 shrink-0" />
              {label}
            </NavLink>
          ))}
        </nav>

        {/* User footer */}
        <div className="flex items-center gap-3 border-t border-[#2a3042] px-4 py-3">
          <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-blue-600 text-xs font-semibold text-white">
            A
          </div>
          <div className="min-w-0">
            <div className="truncate text-sm font-medium text-white">Admin</div>
            <div className="truncate text-xs text-[#8892a4]">Network Admin</div>
          </div>
        </div>
      </aside>
    </>
  )
}
