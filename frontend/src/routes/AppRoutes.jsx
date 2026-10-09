import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import DashboardPage from "../pages/DashboardPage";
import EndpointsPage from "../pages/EndpointsPage";
import ReportsPage from "../pages/ReportsPage";
import Navbar from "../components/Layout/Navbar";
import IncidentsPage from "../pages/IncidentsPage";
import RcaDetailPage from "../pages/RcaDetailPage";
import AgentsPage from "../pages/AgentsPage";
import OperationsPage from "../pages/OperationsPage";
import NotificationsPage from "../pages/NotificationsPage";
import ContainersPage from "../pages/ContainersPage";
import WorkflowExecutionPage from "../pages/WorkflowExecutionPage";

export default function AppRoutes() {
  return (
    <BrowserRouter>
      <Navbar />
      <Routes>
        <Route path="/dashboard" element={<DashboardPage />} />
        <Route path="/endpoints" element={<EndpointsPage />} />
        <Route path="/reports" element={<ReportsPage />} />
        <Route path="/incidents" element={<IncidentsPage />} />
        <Route path="/agents" element={<AgentsPage />} />
        <Route path="/operations" element={<OperationsPage />} />
        <Route path="/operations/workflows/:workflowId" element={<WorkflowExecutionPage />} />
        <Route path="/notifications" element={<NotificationsPage />} />
        <Route path="/containers" element={<ContainersPage />} />
        <Route path="/incidents/:incidentId/rca" element={<RcaDetailPage />} />
        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Routes>
    </BrowserRouter>
  );
}