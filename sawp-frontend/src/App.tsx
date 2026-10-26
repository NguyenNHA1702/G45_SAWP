import { BrowserRouter, Navigate, Route, Routes } from 'react-router';
import { Toaster } from 'react-hot-toast';
import LoginPage from './pages/Auth/LoginPage';
import DashboardPage from './pages/Dashboard/DashboardPage';
import AttendancePage from './pages/TeacherPortal/AttendancePage';
import ChildAttendancePage from './pages/ParentPortal/ChildAttendancePage';
import MealHeadcountPage from './pages/ServiceStaffPortal/MealHeadcountPage';
import StudentsPage from './pages/OfficeStaffPortal/StudentsPage';
import ClassesPage from './pages/OfficeStaffPortal/ClassesPage';
import LeaveRequestPage from './pages/ParentPortal/LeaveRequestPage';
import LeaveApprovalPage from './pages/TeacherPortal/LeaveApprovalPage';
import LinkRequestsPage from './pages/OfficeStaffPortal/LinkRequestsPage';
import HomeroomRosterPage from './pages/TeacherPortal/HomeroomRosterPage';
import ChildDashboardPage from './pages/ParentPortal/ChildDashboardPage';
import AdministrativeRequestsPage from './pages/ParentPortal/AdministrativeRequestsPage';
import AdminRequestsManagementPage from './pages/OfficeStaffPortal/AdminRequestsManagementPage';
import AdministrativeApprovalPage from './pages/SchoolBoardPortal/AdministrativeApprovalPage';
import { MainLayout } from './layouts/MainLayout';
import { TOKEN_KEY } from './services/apiClient';

function RequireAuth({ children }: { children: React.ReactNode }) {
  const token = localStorage.getItem(TOKEN_KEY);
  if (!token) {
    return <Navigate to="/login" replace />;
  }
  return <>{children}</>;
}

export default function App() {
  return (
    <BrowserRouter>
      <Toaster
        position="top-right"
        toastOptions={{
          style: {
            border: '2px solid #1E293B',
            borderRadius: 16,
            boxShadow: '4px 4px 0 0 #1E293B',
            fontFamily: "'Plus Jakarta Sans', system-ui, sans-serif",
            fontWeight: 600,
          },
        }}
      />
      <Routes>
        <Route path="/login" element={<LoginPage />} />

        {/* Các trang sau khi đăng nhập bọc trong MainLayout */}
        <Route
          element={
            <RequireAuth>
              <MainLayout />
            </RequireAuth>
          }
        >
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/teacher/attendance" element={<AttendancePage />} />
          <Route path="/teacher/leave-requests" element={<LeaveApprovalPage />} />
          <Route path="/parent/attendance" element={<ChildAttendancePage />} />
          <Route path="/parent/leave-requests" element={<LeaveRequestPage />} />
          <Route path="/parent/requests" element={<AdministrativeRequestsPage />} />
          <Route path="/service/headcount" element={<MealHeadcountPage />} />
          <Route path="/students" element={<StudentsPage />} />
          <Route path="/classes" element={<ClassesPage />} />
          <Route path="/link-requests" element={<LinkRequestsPage />} />
          <Route path="/office/requests" element={<AdminRequestsManagementPage />} />
          <Route path="/board/requests" element={<AdministrativeApprovalPage />} />
          <Route path="/teacher/roster" element={<HomeroomRosterPage />} />
          <Route path="/parent/children" element={<ChildDashboardPage />} />
        </Route>

        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
