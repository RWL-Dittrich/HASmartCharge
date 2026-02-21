import { TopBar } from '@/components/layout/TopBar'

export function AnalyticsPage() {
  return (
    <div className="flex flex-col h-full">
      <TopBar title="Energy Management" subtitle="Historical charging sessions and analytics" />
      <div className="flex-1 flex items-center justify-center text-[#8892a4]">
        Analytics — coming soon
      </div>
    </div>
  )
}

