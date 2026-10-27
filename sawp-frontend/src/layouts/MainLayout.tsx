import { useEffect, useState, useRef } from 'react';
import { Link, Outlet, useLocation, useNavigate } from 'react-router';
import {
  Bell,
  CalendarCheck2,
  ChevronRight,
  ClipboardList,
  FileCheck,
  FileText,
  GraduationCap,
  HeartHandshake,
  KeyRound,
  LayoutDashboard,
  LogOut,
  Newspaper,
  School,
  Sparkles,
  User,
  Users,
  UtensilsCrossed,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { TOKEN_KEY, USER_KEY } from '../services/apiClient';
import { notificationApi } from '../services/implementations/notificationApi';
import { SYSTEM_MESSAGES } from '../constants/messages';
import { ChangePasswordModal } from '../components/auth/ChangePasswordModal';
import { ParentChildProvider } from '../contexts/ParentChildContext';
import { ChildSelectorPill } from '../components/parent/ChildSelectorPill';
import type { AppNotification } from '../types/notification.types';
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
  // --- WF1: Lớp chủ nhiệm & Học sinh (Chỉ Teacher) ---
  {
    label: 'Lớp chủ nhiệm (WF1)',
    path: '/teacher/roster',
    icon: GraduationCap,
    roles: ['Teacher'],
  },
  // --- Điểm danh (Teacher, OfficeStaff, Admin) ---
  {
    label: 'Điểm danh lớp',
    path: '/teacher/attendance',
    icon: CalendarCheck2,
    roles: ['Teacher', 'OfficeStaff', 'Admin'],
  },
  {
    label: 'Duyệt đơn xin nghỉ',
    path: '/teacher/leave-requests',
    icon: FileCheck,
    roles: ['Teacher', 'SchoolBoard', 'Admin'],
  },
  {
    label: 'Sổ liên lạc lớp (WF6)',
    path: '/teacher/contact-book',
    icon: Newspaper,
    roles: ['Teacher'],
  },
  // --- WF1: Cổng phụ huynh theo dõi con ---
  {
    label: 'Hồ sơ các con (WF1)',
    path: '/parent/children',
    icon: HeartHandshake,
    roles: ['Parent'],
  },
  {
    label: 'Sổ liên lạc điện tử (WF6)',
    path: '/parent/contact-book',
    icon: Newspaper,
    roles: ['Parent'],
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
    label: 'Hồ sơ học sinh (WF1)',
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
  {
    label: 'Duyệt liên kết PH (WF1)',
    path: '/link-requests',
    icon: HeartHandshake,
    roles: ['OfficeStaff', 'Admin'],
  },
];

export function MainLayout() {
  const navigate = useNavigate();
  const location = useLocation();
  const [changePasswordOpen, setChangePasswordOpen] = useState(false);
  const [notifications, setNotifications] = useState<AppNotification[]>([]);
  const [showNotifs, setShowNotifs] = useState(false);
  const notifRef = useRef<HTMLDivElement>(null);

  // Lấy thông tin user hiện tại từ localStorage
  const userJson = localStorage.getItem(USER_KEY);
  const currentUser = userJson ? JSON.parse(userJson) : null;
  const userRoles: string[] = currentUser?.roles ?? [];

  const fetchNotifications = async () => {
    try {
      const page = await notificationApi.getPaged(1, 20);
      setNotifications(page.items);
    } catch {
      // silent
    }
  };

  useEffect(() => {
    fetchNotifications();
    const interval = setInterval(fetchNotifications, 15000);
    return () => clearInterval(interval);
  }, []);

  // Close notifications on outside click
  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (notifRef.current && !notifRef.current.contains(event.target as Node)) {
        setShowNotifs(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const unreadCount = notifications.filter((n) => !n.isRead).length;

  const handleOpenNotification = async (item: AppNotification) => {
    if (!item.isRead) {
      await notificationApi.markRead(item.id).catch(() => null);
      setNotifications((prev) => prev.map((n) => (n.id === item.id ? { ...n, isRead: true } : n)));
    }
    setShowNotifs(false);
    if (item.linkUrl) {
      navigate(item.linkUrl);
    }
  };

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
    <ParentChildProvider userId={currentUser?.id} userRoles={userRoles}>
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
              {userRoles.includes('Parent') && <ChildSelectorPill />}
            </div>

            <div className="header-right">
              {/* NOTIFICATION BUTTON & POPUP */}
              <div style={{ position: 'relative' }} ref={notifRef}>
                <button
                  type="button"
                  className="header-btn"
                  onClick={() => {
                    setShowNotifs(!showNotifs);
                    if (!showNotifs) fetchNotifications();
                  }}
                  title="Thông báo hệ thống"
                  style={{ position: 'relative' }}
                >
                  <Bell size={18} />
                  {unreadCount > 0 ? (
                    <span
                      style={{
                        position: 'absolute',
                        top: '2px',
                        right: '2px',
                        backgroundColor: '#EF4444',
                        color: '#FFFFFF',
                        fontSize: '10px',
                        fontWeight: 800,
                        borderRadius: '10px',
                        padding: '1px 5px',
                        lineHeight: '1.2',
                      }}
                    >
                      {unreadCount > 9 ? '9+' : unreadCount}
                    </span>
                  ) : (
                    <span className="header-badge-dot" />
                  )}
                </button>

                {showNotifs && (
                  <div
                    style={{
                      position: 'absolute',
                      top: '46px',
                      right: 0,
                      width: '360px',
                      maxHeight: '440px',
                      overflowY: 'auto',
                      backgroundColor: '#FFFFFF',
                      borderRadius: '16px',
                      boxShadow: '0 12px 30px rgba(0, 0, 0, 0.15)',
                      border: '1px solid #E2E8F0',
                      zIndex: 9999,
                      padding: '16px',
                    }}
                  >
                    <div
                      style={{
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                        marginBottom: '12px',
                        paddingBottom: '8px',
                        borderBottom: '1px solid #F1F5F9',
                      }}
                    >
                      <strong style={{ fontSize: '15px', color: '#0F172A' }}>Thông báo hệ thống</strong>
                      <span style={{ fontSize: '12px', color: '#64748B' }}>
                        {unreadCount} chưa đọc
                      </span>
                    </div>

                    {notifications.length === 0 ? (
                      <div style={{ textAlign: 'center', padding: '24px 8px', color: '#94A3B8', fontSize: '13px' }}>
                        Không có thông báo nào
                      </div>
                    ) : (
                      <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                        {notifications.map((item) => (
                          <div
                            key={item.id}
                            onClick={() => handleOpenNotification(item)}
                            style={{
                              padding: '10px 12px',
                              borderRadius: '10px',
                              backgroundColor: item.isRead ? '#F8FAFC' : '#EFF6FF',
                              border: item.isRead ? '1px solid #F1F5F9' : '1px solid #BFDBFE',
                              cursor: 'pointer',
                              transition: 'background-color 0.15s ease',
                            }}
                          >
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '4px' }}>
                              <strong style={{ fontSize: '13px', color: item.isRead ? '#334155' : '#1D4ED8' }}>
                                {item.title}
                              </strong>
                              {!item.isRead && (
                                <span style={{ width: '8px', height: '8px', borderRadius: '50%', backgroundColor: '#2563EB', display: 'inline-block' }} />
                              )}
                            </div>
                            <p style={{ margin: 0, fontSize: '12px', color: '#475569', lineHeight: 1.4 }}>
                              {item.content}
                            </p>
                            <small style={{ fontSize: '11px', color: '#94A3B8', marginTop: '4px', display: 'block' }}>
                              {new Date(item.createdAt).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })}{' '}
                              {new Date(item.createdAt).toLocaleDateString('vi-VN')}
                            </small>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                )}
              </div>

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
    </ParentChildProvider>
  );
}
