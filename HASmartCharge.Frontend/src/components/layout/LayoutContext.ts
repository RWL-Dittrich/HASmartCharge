import { createContext, useContext } from 'react'

interface SidebarContextValue {
  /** Opens the mobile navigation drawer. No-op on desktop where the sidebar is always visible. */
  open: () => void
}

export const SidebarContext = createContext<SidebarContextValue>({ open: () => {} })

export const useSidebar = () => useContext(SidebarContext)
