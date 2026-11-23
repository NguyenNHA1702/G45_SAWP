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
import HomeroomRosterPage from './pages/TeacherPortal/HomeroomRosterPage';
import TeacherBoardingPage from './pages/TeacherPortal/TeacherBoardingPage';
import ChildDashboardPage from './pages/ParentPortal/ChildDashboardPage';
import AdministrativeRequestsPage from './pages/ParentPortal/AdministrativeRequestsPage';
import AdminRequestsManagementPage from './pages/OfficeStaffPortal/AdminRequestsManagementPage';
import AdministrativeApprovalPage from './pages/SchoolBoardPortal/AdministrativeApprovalPage';
import ParentLunchPage from './pages/ParentPortal/ParentLunchPage';
import ProfilePage from './pages/Auth/ProfilePage';
import EContactBookPage from './pages/ParentPortal/EContactBookPage';
import TeacherContactBookPage from './pages/TeacherPortal/TeacherContactBookPage';
import AuditLogsPage from './pages/SystemPortal/AuditLogsPage';
import UsersManagementPage from './pages/AdminPortal/UsersManagementPage';
import SystemConfigPage from './pages/AdminPortal/SystemConfigPage';
import { MainLayout } from './layouts/MainLayout';
import { clearAuthSession, TOKEN_KEY } from './services/apiClient';

function isTokenExpired(token: string): boolean {
  try {
    const base64Url = token.split('.')[1];
    if (!base64Url) return true;
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    );
    const payload = JSON.parse(jsonPayload);
    if (!payload.exp) return false;
    return Date.now() >= payload.exp * 1000;
  } catch {
    return true;
  }
}

function RequireAuth({ children }: { children: React.ReactNode }) {
  const token = sessionStorage.getItem(TOKEN_KEY);
  if (!token || isTokenExpired(token)) {
    clearAuthSession();
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
        <Route path="/" element={<Navigate to="/login" replace />} />
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
          <Route path="/teacher/contact-book" element={<TeacherContactBookPage />} />
          <Route path="/parent/attendance" element={<ChildAttendancePage />} />
          <Route path="/parent/leave-requests" element={<LeaveRequestPage />} />
          <Route path="/parent/requests" element={<AdministrativeRequestsPage />} />
          <Route path="/parent/lunch" element={<ParentLunchPage />} />
          <Route path="/parent/contact-book" element={<EContactBookPage />} />
          <Route path="/service/headcount" element={<MealHeadcountPage />} />
          <Route path="/students" element={<StudentsPage />} />
          <Route path="/classes" element={<ClassesPage />} />
          <Route path="/office/requests" element={<AdminRequestsManagementPage />} />
          <Route path="/board/requests" element={<AdministrativeApprovalPage />} />
          <Route path="/teacher/roster" element={<HomeroomRosterPage />} />
          <Route path="/teacher/boarding" element={<TeacherBoardingPage />} />
          <Route path="/parent/children" element={<ChildDashboardPage />} />
          <Route path="/system/audit-logs" element={<AuditLogsPage />} />
          <Route path="/admin/users" element={<UsersManagementPage />} />
          <Route path="/admin/config" element={<SystemConfigPage />} />
          <Route path="/profile" element={<ProfilePage />} />
        </Route>

        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
