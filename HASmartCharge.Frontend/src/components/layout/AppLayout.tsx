import { useState } from 'react'
import { Outlet } from 'react-router'
import { Sidebar } from './Sidebar'
import { SidebarContext } from './LayoutContext'

export function AppLayout() {
  const [sidebarOpen, setSidebarOpen] = useState(false)

  return (
    <SidebarContext.Provider value={{ open: () => setSidebarOpen(true) }}>
      <div className="flex h-screen overflow-hidden bg-[#0f1117]">
        <Sidebar open={sidebarOpen} onClose={() => setSidebarOpen(false)} />
        <main className="flex flex-1 flex-col overflow-hidden min-w-0">
          <Outlet />
        </main>
      </div>
    </SidebarContext.Provider>
  )
}
