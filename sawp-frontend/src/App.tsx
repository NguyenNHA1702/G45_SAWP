import { BrowserRouter, Navigate, Route, Routes } from 'react-router';
import toast, { Toaster } from 'react-hot-toast';
import LoginPage from './pages/Auth/LoginPage';
import ForgotPasswordPage from './pages/Auth/ForgotPasswordPage';
import SetPasswordPage from './pages/Auth/SetPasswordPage';
import ChangeTemporaryPasswordPage from './pages/Auth/ChangeTemporaryPasswordPage';
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
import StaffListPage from './pages/AdminPortal/StaffListPage';
import AccountRequestReviewPage from './pages/AdminPortal/AccountRequestReviewPage';
import AccountRequestsPage from './pages/OfficeStaffPortal/AccountRequestsPage';
import SystemConfigPage from './pages/AdminPortal/SystemConfigPage';
import { MainLayout } from './layouts/MainLayout';
import { clearAuthSession, getAuthUser, TOKEN_KEY } from './services/apiClient';

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

/** True when the signed-in account still has to replace a temporary password (UC-60 A1). */
function mustChangePassword(): boolean {
  try {
    return JSON.parse(getAuthUser() ?? '{}')?.mustChangePassword === true;
  } catch {
    return false;
  }
}

function RequireAuth({ children }: { children: React.ReactNode }) {
  const token = sessionStorage.getItem(TOKEN_KEY);
  if (!token || isTokenExpired(token)) {
    clearAuthSession();
    return <Navigate to="/login" replace />;
  }
  if (mustChangePassword()) {
    return <Navigate to="/change-password" replace />;
  }
  return <>{children}</>;
}

/** Chặn phân quyền truy cập Route theo vai trò người dùng (Role-based Route Guard) */
function RequireRole({ roles, children }: { roles: string[]; children: React.ReactNode }) {
  const userJson = getAuthUser();
  let currentUser: any = null;
  try {
    currentUser = userJson ? JSON.parse(userJson) : null;
  } catch {
    currentUser = null;
  }
  const rawRoles = currentUser?.roles ?? (currentUser?.role ? [currentUser.role] : []);
  const userRoles: string[] = Array.isArray(rawRoles) ? rawRoles : [String(rawRoles)];

  const hasRole = roles.some((r) =>
    userRoles.some((ur) => typeof ur === 'string' && ur.toLowerCase() === r.toLowerCase())
  );
  if (!hasRole) {
    toast.error('Bạn không có quyền truy cập trang này.');
    return <Navigate to="/dashboard" replace />;
  }
  return <>{children}</>;
}

/** Signed in, but without the temporary-password redirect (the Change Password page itself). */
function RequireToken({ children }: { children: React.ReactNode }) {
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
        {/* UC-66: public pages opened from the Login page or from emailed links */}
        <Route path="/forgot-password" element={<ForgotPasswordPage />} />
        <Route path="/reset-password" element={<SetPasswordPage />} />
        <Route path="/activate" element={<SetPasswordPage />} />
        <Route
          path="/change-password"
          element={
            <RequireToken>
              <ChangeTemporaryPasswordPage />
            </RequireToken>
          }
        />

        {/* Các trang sau khi đăng nhập bọc trong MainLayout */}
        <Route
          element={
            <RequireAuth>
              <MainLayout />
            </RequireAuth>
          }
        >
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route
            path="/teacher/attendance"
            element={
              <RequireRole roles={['Teacher', 'OfficeStaff', 'SchoolBoard', 'Admin']}>
                <AttendancePage />
              </RequireRole>
            }
          />
          <Route
            path="/teacher/leave-requests"
            element={
              <RequireRole roles={['Teacher', 'SchoolBoard', 'Admin']}>
                <LeaveApprovalPage />
              </RequireRole>
            }
          />
          <Route
            path="/teacher/contact-book"
            element={
              <RequireRole roles={['Teacher']}>
                <TeacherContactBookPage />
              </RequireRole>
            }
          />
          <Route
            path="/parent/attendance"
            element={
              <RequireRole roles={['Parent', 'Admin']}>
                <ChildAttendancePage />
              </RequireRole>
            }
          />
          <Route
            path="/parent/leave-requests"
            element={
              <RequireRole roles={['Parent', 'Admin']}>
                <LeaveRequestPage />
              </RequireRole>
            }
          />
          <Route
            path="/parent/requests"
            element={
              <RequireRole roles={['Parent', 'Admin']}>
                <AdministrativeRequestsPage />
              </RequireRole>
            }
          />
          <Route
            path="/parent/lunch"
            element={
              <RequireRole roles={['Parent', 'Admin']}>
                <ParentLunchPage />
              </RequireRole>
            }
          />
          <Route
            path="/parent/contact-book"
            element={
              <RequireRole roles={['Parent', 'Admin']}>
                <EContactBookPage />
              </RequireRole>
            }
          />
          <Route
            path="/service/headcount"
            element={
              <RequireRole roles={['ServiceStaff', 'OfficeStaff', 'SchoolBoard', 'Admin']}>
                <MealHeadcountPage />
              </RequireRole>
            }
          />
          <Route
            path="/students"
            element={
              <RequireRole roles={['OfficeStaff', 'Admin']}>
                <StudentsPage />
              </RequireRole>
            }
          />
          <Route
            path="/classes"
            element={
              <RequireRole roles={['OfficeStaff', 'SchoolBoard', 'Admin']}>
                <ClassesPage />
              </RequireRole>
            }
          />
          <Route
            path="/office/requests"
            element={
              <RequireRole roles={['OfficeStaff', 'Admin']}>
                <AdminRequestsManagementPage />
              </RequireRole>
            }
          />
          <Route
            path="/board/requests"
            element={
              <RequireRole roles={['SchoolBoard', 'Admin']}>
                <AdministrativeApprovalPage />
              </RequireRole>
            }
          />
          <Route
            path="/teacher/roster"
            element={
              <RequireRole roles={['Teacher', 'Admin']}>
                <HomeroomRosterPage />
              </RequireRole>
            }
          />
          <Route
            path="/teacher/boarding"
            element={
              <RequireRole roles={['Teacher', 'Admin']}>
                <TeacherBoardingPage />
              </RequireRole>
            }
          />
          <Route
            path="/parent/children"
            element={
              <RequireRole roles={['Parent', 'Admin']}>
                <ChildDashboardPage />
              </RequireRole>
            }
          />
          <Route
            path="/system/audit-logs"
            element={
              <RequireRole roles={['OfficeStaff', 'SchoolBoard', 'Admin']}>
                <AuditLogsPage />
              </RequireRole>
            }
          />
          <Route
            path="/admin/users"
            element={
              <RequireRole roles={['Admin']}>
                <UsersManagementPage />
              </RequireRole>
            }
          />
          <Route
            path="/staff"
            element={
              <RequireRole roles={['SchoolBoard', 'Admin']}>
                <StaffListPage />
              </RequireRole>
            }
          />
          <Route
            path="/admin/account-requests"
            element={
              <RequireRole roles={['Admin']}>
                <AccountRequestReviewPage />
              </RequireRole>
            }
          />
          <Route
            path="/office/account-requests"
            element={
              <RequireRole roles={['OfficeStaff', 'Admin']}>
                <AccountRequestsPage />
              </RequireRole>
            }
          />
          <Route
            path="/admin/config"
            element={
              <RequireRole roles={['Admin', 'OfficeStaff']}>
                <SystemConfigPage />
              </RequireRole>
            }
          />
          <Route path="/profile" element={<ProfilePage />} />
        </Route>

        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
