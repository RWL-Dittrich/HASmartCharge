import { NavLink } from 'react-router'
import {
  LayoutDashboard,
  Zap,
  Users,
  BarChart3,
  Settings,
  BatteryCharging,
} from 'lucide-react'
import { cn } from '@/lib/utils'

const navItems = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard },
  { to: '/chargers', label: 'Charging Points', icon: Zap },
  { to: '/users', label: 'Users', icon: Users },
  { to: '/analytics', label: 'Energy Management', icon: BarChart3 },
  { to: '/settings', label: 'Settings', icon: Settings },
]

export function Sidebar() {
  return (
    <aside className="flex h-full w-56 flex-col bg-[#1a1f2e] border-r border-[#2a3042]">
      {/* Logo */}
      <div className="flex items-center gap-3 px-4 py-5 border-b border-[#2a3042]">
        <div className="flex h-8 w-8 items-center justify-center rounded-md bg-blue-600">
          <BatteryCharging className="h-4 w-4 text-white" />
        </div>
        <div>
          <div className="text-sm font-semibold text-white">HA Smart Charge</div>
          <div className="text-xs text-[#8892a4]">OCPP Admin Console</div>
        </div>
      </div>

      {/* Nav */}
      <nav className="flex-1 px-2 py-3 space-y-0.5">
        {navItems.map(({ to, label, icon: Icon }) => (
          <NavLink
            key={to}
            to={to}
            end={to === '/'}
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
        <div className="flex h-8 w-8 items-center justify-center rounded-full bg-blue-600 text-xs font-semibold text-white">
          A
        </div>
        <div className="min-w-0">
          <div className="truncate text-sm font-medium text-white">Admin</div>
          <div className="truncate text-xs text-[#8892a4]">Network Admin</div>
        </div>
      </div>
    </aside>
  )
}

