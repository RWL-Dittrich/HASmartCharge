import { Routes, Route } from "react-router"
import { AppLayout } from "@/components/layout/AppLayout"
import { DashboardPage } from "@/pages/DashboardPage"
import { SchedulePage } from "@/pages/SchedulePage"
import { SettingsPage } from "@/pages/SettingsPage"
import { HistoryPage } from "@/pages/HistoryPage"
export default function App() {
  return (
    <Routes>
      <Route element={<AppLayout />}>
        <Route index element={<DashboardPage />} />
        <Route path="schedule" element={<SchedulePage />} />
        <Route path="settings" element={<SettingsPage />} />
        <Route path="history" element={<HistoryPage />} />
      </Route>
    </Routes>
  )
}
