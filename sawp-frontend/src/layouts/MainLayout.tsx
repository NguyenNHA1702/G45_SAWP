import { useState } from 'react';
import { Link, Outlet, useLocation, useNavigate } from 'react-router';
import {
  Bell,
  CalendarCheck2,
  ChevronRight,
  ClipboardList,
  FileCheck,
  FileText,
  GraduationCap,
  KeyRound,
  LayoutDashboard,
  LogOut,
  School,
  Sparkles,
  User,
  Users,
  UtensilsCrossed,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { TOKEN_KEY, USER_KEY } from '../services/apiClient';
import { SYSTEM_MESSAGES } from '../constants/messages';
import { ChangePasswordModal } from '../components/auth/ChangePasswordModal';
import './MainLayout.css';

interface NavItem {
  label: string;
  path: string;
  icon: typeof LayoutDashboard;
  roles?: string[];
  badge?: string;
}

const NAV_ITEMS: NavItem[] = [
  {
    label: 'Tổng quan',
    path: '/dashboard',
    icon: LayoutDashboard,
  },
  // --- Điểm danh (Chỉ Teacher) ---
  {
    label: 'Điểm danh lớp',
    path: '/teacher/attendance',
    icon: CalendarCheck2,
    roles: ['Teacher'],
  },
  {
    label: 'Duyệt đơn xin nghỉ',
    path: '/teacher/leave-requests',
    icon: FileCheck,
    roles: ['Teacher'],
  },
  // --- Phụ huynh theo dõi chuyên cần (Chỉ Parent) ---
  {
    label: 'Đơn xin nghỉ học',
    path: '/parent/leave-requests',
    icon: FileText,
    roles: ['Parent'],
  },
  {
    label: 'Chuyên cần của con',
    path: '/parent/attendance',
    icon: ClipboardList,
    roles: ['Parent'],
  },
  // --- Bếp ăn bán trú (ServiceStaff, Teacher, Admin) ---
  {
    label: 'Báo cáo suất ăn Bếp',
    path: '/service/headcount',
    icon: UtensilsCrossed,
    roles: ['ServiceStaff', 'Teacher', 'Admin'],
  },
  // --- Giáo vụ quản lý học sinh & phân lớp (OfficeStaff, Admin) ---
  {
    label: 'Hồ sơ học sinh',
    path: '/students',
    icon: Users,
    roles: ['OfficeStaff', 'Admin'],
  },
  {
    label: 'Lớp học & Phân công',
    path: '/classes',
    icon: GraduationCap,
    roles: ['OfficeStaff', 'Admin'],
  },
];

export function MainLayout() {
  const navigate = useNavigate();
  const location = useLocation();
  const [changePasswordOpen, setChangePasswordOpen] = useState(false);

  // Lấy thông tin user hiện tại từ localStorage
  const userJson = localStorage.getItem(USER_KEY);
  const currentUser = userJson ? JSON.parse(userJson) : null;
  const userRoles: string[] = currentUser?.roles ?? [];

  const handleLogout = () => {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    toast.success(SYSTEM_MESSAGES.MSG06.content);
    navigate('/login');
  };

  // Lọc menu theo roles
  const allowedNav = NAV_ITEMS.filter((item) => {
    if (!item.roles || item.roles.length === 0) return true;
    return item.roles.some((r) => userRoles.includes(r));
  });

  return (
    <div className="layout-root">
      {/* SIDEBAR */}
      <aside className="layout-sidebar">
        <div className="sidebar-brand">
          <div className="brand-icon">
            <School size={24} color="#1E293B" />
          </div>
          <div>
            <div className="brand-title">
              SAWP <Sparkles size={14} className="brand-sparkle" />
            </div>
            <div className="brand-subtitle">Primary School</div>
          </div>
        </div>

        <nav className="sidebar-nav">
          <div className="nav-section-label">CHỨC NĂNG CHÍNH</div>
          {allowedNav.map((item) => {
            const Icon = item.icon;
            const isActive =
              item.path === '/dashboard'
                ? location.pathname === '/dashboard'
                : location.pathname.startsWith(item.path);
            return (
              <Link
                key={item.path}
                to={item.path}
                className={`nav-link ${isActive ? 'nav-link--active' : ''}`}
              >
                <span className="nav-link__icon">
                  <Icon size={18} />
                </span>
                <span className="nav-link__label">{item.label}</span>
                {item.badge && <span className="badge badge--pink nav-badge">{item.badge}</span>}
                {isActive && <ChevronRight size={16} className="nav-chevron" />}
              </Link>
            );
          })}
        </nav>

        <div className="sidebar-footer">
          <div className="user-card card">
            <div className="user-avatar" aria-hidden="true">
              {currentUser?.fullName?.[0]?.toUpperCase() ?? 'U'}
            </div>
            <div className="user-info">
              <div className="user-name">{currentUser?.fullName ?? 'Người dùng'}</div>
              <div className="user-role-badge">
                <span className="badge badge--violet badge--sm">
                  {userRoles[0] ?? 'Thành viên'}
                </span>
              </div>
            </div>
          </div>

          <div className="sidebar-actions">
            <button
              type="button"
              className="btn btn--secondary btn--sm btn--full"
              onClick={() => setChangePasswordOpen(true)}
              title="Đổi mật khẩu tài khoản"
            >
              <KeyRound size={15} /> Đổi mật khẩu
            </button>
            <button
              type="button"
              className="btn btn--secondary btn--sm btn--full btn--logout"
              onClick={handleLogout}
            >
              <LogOut size={15} /> Đăng xuất
            </button>
          </div>
        </div>
      </aside>

      {/* MAIN WRAPPER */}
      <div className="layout-main-wrapper">
        {/* HEADER */}
        <header className="layout-header">
          <div className="header-left">
            <span className="badge badge--amber header-pill">Năm học 2026 - 2027</span>
          </div>

          <div className="header-right">
            <button
              type="button"
              className="header-btn"
              onClick={() => toast('Chức năng thông báo đang kết nối')}
              title="Thông báo"
            >
              <Bell size={18} />
              <span className="header-badge-dot" />
            </button>

            <div className="header-user">
              <span className="user-greeting">
                Xin chào, <strong>{currentUser?.fullName ?? 'Thầy/Cô'}</strong>
              </span>
              <div className="header-avatar">
                <User size={16} />
              </div>
            </div>
          </div>
        </header>

        {/* CONTENT VIEW */}
        <main className="layout-content">
          <Outlet />
        </main>
      </div>

      {/* Modal đổi mật khẩu */}
      <ChangePasswordModal
        open={changePasswordOpen}
        onClose={() => setChangePasswordOpen(false)}
      />
    </div>
  );
}
