import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import DashboardPage from "../pages/DashboardPage";
import EndpointsPage from "../pages/EndpointsPage";
import ReportsPage from "../pages/ReportsPage";
import Navbar from "../components/Layout/Navbar";

export default function AppRoutes() {
  return (
    <BrowserRouter>
      <Navbar />
      <Routes>
        <Route path="/dashboard" element={<DashboardPage />} />
        <Route path="/endpoints" element={<EndpointsPage />} />
        <Route path="/reports" element={<ReportsPage />} />
        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Routes>
    </BrowserRouter>
  );
}