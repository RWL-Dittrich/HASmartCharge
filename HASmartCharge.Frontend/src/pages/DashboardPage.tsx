import { Zap, CheckCircle, Activity, AlertTriangle } from 'lucide-react'
import { TopBar } from '@/components/layout/TopBar'
import { StatCard } from '@/components/ui/StatCard'

const recentActivity = [
  { id: 'CP-EU-8821', location: 'Berlin Hub 04', status: 'Available', time: '2m ago' },
  { id: 'CP-US-1029', location: 'LAX Term 1 Parking', status: 'Charging', time: '5m ago' },
  { id: 'CP-UK-4412', location: 'London East Mall', status: 'Faulted', time: '8m ago' },
  { id: 'CP-FR-2900', location: 'Paris Gare du Nord', status: 'Finishing', time: '12m ago' },
]

const statusColour: Record<string, string> = {
  Available: 'text-emerald-400',
  Charging: 'text-blue-400',
  Faulted: 'text-red-400',
  Finishing: 'text-yellow-400',
}

export function DashboardPage() {
  return (
    <div className="flex flex-col h-full overflow-auto">
      <TopBar title="Dashboard" subtitle="Real-time status of your charging infrastructure" />

      <div className="flex-1 p-6 space-y-6">
        {/* Stat cards */}
        <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
          <StatCard
            title="Total Chargers"
            value="12"
            change="+2 this month"
            changePositive
          />
          <StatCard
            title="Available Now"
            value="8"
            change="+5.1%"
            changePositive
          />
          <StatCard
            title="Active Sessions"
            value="3"
            change="Peak: 4"
            changePositive
          />
          <StatCard
            title="System Faults"
            value="1"
            change="Action Required"
            changePositive={false}
          />
        </div>

        {/* Quick-status banner */}
        <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
          {[
            { label: 'Online', value: 11, icon: Activity, colour: 'text-emerald-400', bg: 'bg-emerald-400/10' },
            { label: 'Charging', value: 3, icon: Zap, colour: 'text-blue-400', bg: 'bg-blue-400/10' },
            { label: 'Available', value: 8, icon: CheckCircle, colour: 'text-emerald-400', bg: 'bg-emerald-400/10' },
            { label: 'Faulted', value: 1, icon: AlertTriangle, colour: 'text-red-400', bg: 'bg-red-400/10' },
          ].map(({ label, value, icon: Icon, colour, bg }) => (
            <div key={label} className="flex items-center gap-3 rounded-lg bg-[#1a1f2e] border border-[#2a3042] p-4">
              <div className={`flex h-10 w-10 items-center justify-center rounded-full ${bg}`}>
                <Icon className={`h-5 w-5 ${colour}`} />
              </div>
              <div>
                <div className={`text-xl font-bold ${colour}`}>{value}</div>
                <div className="text-xs text-[#8892a4]">{label}</div>
              </div>
            </div>
          ))}
        </div>

        {/* Recent activity */}
        <div className="rounded-lg bg-[#1a1f2e] border border-[#2a3042]">
          <div className="flex items-center justify-between border-b border-[#2a3042] px-4 py-3">
            <h2 className="text-sm font-semibold text-white">Recent Activity</h2>
            <a href="/chargers" className="text-xs text-blue-400 hover:text-blue-300">
              View all →
            </a>
          </div>
          <table className="w-full text-sm">
            <thead>
              <tr className="text-xs text-[#8892a4] border-b border-[#2a3042]">
                <th className="px-4 py-2 text-left font-medium">Charger ID</th>
                <th className="px-4 py-2 text-left font-medium">Location</th>
                <th className="px-4 py-2 text-left font-medium">Status</th>
                <th className="px-4 py-2 text-left font-medium">Last Updated</th>
              </tr>
            </thead>
            <tbody>
              {recentActivity.map((row) => (
                <tr key={row.id} className="border-b border-[#2a3042] last:border-0 hover:bg-[#232938] transition-colors">
                  <td className="px-4 py-3 font-mono text-xs text-white">{row.id}</td>
                  <td className="px-4 py-3 text-[#8892a4]">{row.location}</td>
                  <td className={`px-4 py-3 font-medium ${statusColour[row.status] ?? 'text-white'}`}>
                    {row.status}
                  </td>
                  <td className="px-4 py-3 text-[#8892a4]">{row.time}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  )
}

