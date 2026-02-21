import { Routes, Route } from "react-router"
import { AppLayout } from "@/components/layout/AppLayout"
import { DashboardPage } from "@/pages/DashboardPage"
import { ChargersPage } from "@/pages/ChargersPage"
import { AnalyticsPage } from "@/pages/AnalyticsPage"
export default function App() {
  return (
    <Routes>
      <Route element={<AppLayout />}>
        <Route index element={<DashboardPage />} />
        <Route path="chargers" element={<ChargersPage />} />
        <Route path="analytics" element={<AnalyticsPage />} />
      </Route>
    </Routes>
  )
}
