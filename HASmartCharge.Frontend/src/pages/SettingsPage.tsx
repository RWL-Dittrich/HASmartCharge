import { useState } from 'react'
import { TopBar } from '@/components/layout/TopBar'
import { cn } from '@/lib/utils'
import { PriceProviderTab } from '@/pages/settings/PriceProviderTab'
import { CarTab } from '@/pages/settings/CarTab'
import { ChargerTab } from '@/pages/settings/ChargerTab'
import { HomeAssistantTab } from '@/pages/settings/HomeAssistantTab'
import { MqttTab } from '@/pages/settings/MqttTab'

const TABS = [
  { id: 'price', label: 'Price Provider', Component: PriceProviderTab },
  { id: 'car', label: 'Car', Component: CarTab },
  { id: 'charger', label: 'Charger', Component: ChargerTab },
  { id: 'ha', label: 'Home Assistant', Component: HomeAssistantTab },
  { id: 'mqtt', label: 'MQTT', Component: MqttTab },
] as const

export function SettingsPage() {
  const [activeTab, setActiveTab] = useState<(typeof TABS)[number]['id']>('price')
  const ActiveComponent = TABS.find((t) => t.id === activeTab)?.Component ?? PriceProviderTab

  return (
    <div className="flex flex-col h-full overflow-auto">
      <TopBar title="Settings" subtitle="Price provider, car, charger, Home Assistant, and MQTT configuration" />

      <div className="flex-1 p-4 sm:p-6">
        <div className="flex gap-1 border-b border-[#2a3042] mb-6 overflow-x-auto">
          {TABS.map((tab) => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              className={cn(
                'shrink-0 whitespace-nowrap px-3 py-2.5 text-sm font-medium border-b-2 -mb-px transition-colors sm:px-4',
                activeTab === tab.id
                  ? 'border-blue-500 text-white'
                  : 'border-transparent text-[#8892a4] hover:text-white',
              )}
            >
              {tab.label}
            </button>
          ))}
        </div>

        <div className="rounded-lg bg-[#1a1f2e] border border-[#2a3042] p-4 sm:p-5">
          <ActiveComponent />
        </div>
      </div>
    </div>
  )
}
