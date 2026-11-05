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
  LayoutDashboard,
  LogOut,
  School,
  Sparkles,
  User,
  Users,
  UtensilsCrossed,
  ShieldCheck,
  FileSpreadsheet,
  Clock,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { TOKEN_KEY, USER_KEY } from '../services/apiClient';
import { notificationApi } from '../services/implementations/notificationApi';
import { SYSTEM_MESSAGES } from '../constants/messages';
import { ParentChildProvider } from '../contexts/ParentChildContext';
import { ChildSelectorPill } from '../components/parent/ChildSelectorPill';
import type { AppNotification } from '../types/notification.types';
import './MainLayout.css';

export interface NavSubItem {
  id: string;
  label: string;
  path: string;
  roles?: string[];
  badge?: string;
  icon?: typeof LayoutDashboard;
}

export interface NavItem {
  id: string;
  label: string;
  path?: string;
  icon: typeof LayoutDashboard;
  roles?: string[];
  badge?: string;
  children?: NavSubItem[];
}

const NAV_ITEMS: NavItem[] = [
  {
    id: 'dashboard',
    label: 'Tổng quan',
    path: '/dashboard',
    icon: LayoutDashboard,
  },
  // --- Bếp ăn bán trú & Báo cáo (ServiceStaff vận hành; OfficeStaff/SchoolBoard/Admin xem) ---
  {
    id: 'kitchen-reports',
    label: 'Báo cáo suất ăn Bếp',
    icon: UtensilsCrossed,
    roles: ['ServiceStaff', 'OfficeStaff', 'SchoolBoard', 'Admin'],
    children: [
      {
        id: 'kitchen-summary',
        label: 'Tổng hợp & Chốt suất',
        path: '/service/headcount?tab=summary',
        icon: FileSpreadsheet,
      },
      {
        id: 'kitchen-students',
        label: 'Danh sách nhận suất (UC-33)',
        path: '/service/headcount?tab=students',
        icon: Users,
      },
      {
        id: 'kitchen-logs',
        label: 'Nhật ký biến động',
        path: '/service/headcount?tab=logs',
        icon: Clock,
      },
    ],
  },
  // --- Quản lý Giáo vụ: Học sinh & Xếp lớp (OfficeStaff, SchoolBoard, Admin) ---
  {
    id: 'office-students-group',
    label: 'Học sinh & Lớp học',
    icon: Users,
    roles: ['OfficeStaff', 'SchoolBoard', 'Admin'],
    children: [
      {
        id: 'office-students',
        label: 'Hồ sơ học sinh',
        path: '/students',
        roles: ['OfficeStaff', 'Admin'],
        icon: Users,
      },
      {
        id: 'office-classes',
        label: 'Lớp học & Xếp lớp',
        path: '/classes',
        roles: ['OfficeStaff', 'SchoolBoard', 'Admin'],
        icon: GraduationCap,
      },
      {
        id: 'office-link-requests',
        label: 'Duyệt liên kết phụ huynh',
        path: '/link-requests',
        roles: ['OfficeStaff', 'Admin'],
        icon: HeartHandshake,
      },
    ],
  },
  // --- Giáo viên: Điểm danh & Lớp học (Teacher, SchoolBoard) ---
  {
    id: 'teacher-group',
    label: 'Điểm danh & Lớp học',
    icon: CalendarCheck2,
    roles: ['Teacher', 'SchoolBoard'],
    children: [
      {
        id: 'teacher-roster',
        label: 'Lớp chủ nhiệm',
        path: '/teacher/roster',
        roles: ['Teacher'],
        icon: GraduationCap,
      },
      {
        id: 'teacher-attendance',
        label: 'Điểm danh lớp',
        path: '/teacher/attendance',
        roles: ['Teacher'],
        icon: CalendarCheck2,
      },
      {
        id: 'teacher-leave',
        label: 'Duyệt đơn xin nghỉ',
        path: '/teacher/leave-requests',
        roles: ['Teacher', 'SchoolBoard'],
        icon: FileCheck,
      },
    ],
  },
  // --- Cổng phụ huynh (Parent) ---
  {
    id: 'parent-group',
    label: 'Theo dõi con & Bán trú',
    icon: HeartHandshake,
    roles: ['Parent'],
    children: [
      {
        id: 'parent-children',
        label: 'Hồ sơ các con',
        path: '/parent/children',
        icon: HeartHandshake,
      },
      {
        id: 'parent-lunch',
        label: 'Đăng ký bán trú',
        path: '/parent/lunch',
        icon: UtensilsCrossed,
      },
      {
        id: 'parent-leave',
        label: 'Đơn xin nghỉ học',
        path: '/parent/leave-requests',
        icon: FileText,
      },
      {
        id: 'parent-attendance',
        label: 'Chuyên cần của con',
        path: '/parent/attendance',
        icon: ClipboardList,
      },
    ],
  },
  // --- Đơn hành chính & Giấy tờ (Parent, OfficeStaff, SchoolBoard, Admin) ---
  {
    id: 'administrative-requests-group',
    label: 'Đơn từ & Hành chính',
    icon: FileCheck,
    roles: ['Parent', 'OfficeStaff', 'SchoolBoard', 'Admin'],
    children: [
      {
        id: 'parent-requests',
        label: 'Gửi đơn & Giấy tờ',
        path: '/parent/requests',
        roles: ['Parent'],
        icon: FileCheck,
      },
      {
        id: 'office-requests',
        label: 'Xử lý đơn hành chính',
        path: '/office/requests',
        roles: ['OfficeStaff', 'Admin'],
        icon: FileCheck,
      },
      {
        id: 'board-requests',
        label: 'Phê duyệt đơn cấp trường',
        path: '/board/requests',
        roles: ['SchoolBoard', 'Admin'],
        icon: ShieldCheck,
      },
    ],
  },
];

export function MainLayout() {
  const navigate = useNavigate();
  const location = useLocation();
  const [notifications, setNotifications] = useState<AppNotification[]>([]);
  const [showNotifs, setShowNotifs] = useState(false);
  const notifRef = useRef<HTMLDivElement>(null);

  // Expanded submenus state
  const [expandedMenus, setExpandedMenus] = useState<Record<string, boolean>>({
    'kitchen-reports': true,
  });

  // Lấy thông tin user hiện tại từ localStorage và phản hồi khi cập nhật hồ sơ
  const [currentUser, setCurrentUser] = useState(() => {
    const userJson = localStorage.getItem(USER_KEY);
    return userJson ? JSON.parse(userJson) : null;
  });

  useEffect(() => {
    const handleStorageChange = () => {
      const userJson = localStorage.getItem(USER_KEY);
      setCurrentUser(userJson ? JSON.parse(userJson) : null);
    };
    window.addEventListener('storage', handleStorageChange);
    return () => window.removeEventListener('storage', handleStorageChange);
  }, []);

  const userRoles: string[] = currentUser?.roles ?? [];

  const isChildActive = (targetPathWithQuery: string) => {
    const [targetPath, targetQuery] = targetPathWithQuery.split('?');
    if (location.pathname !== targetPath) return false;

    if (targetQuery) {
      return location.search.includes(targetQuery);
    }

    return !location.search || location.search.includes('tab=summary');
  };

  // Tự động mở menu chứa trang hiện tại
  useEffect(() => {
    NAV_ITEMS.forEach((item) => {
      if (item.children) {
        const hasActive = item.children.some((c) => isChildActive(c.path));
        if (hasActive) {
          setExpandedMenus((prev) => (prev[item.id] ? prev : { ...prev, [item.id]: true }));
        }
      }
    });
  }, [location.pathname, location.search]);

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
    if (item.roles && item.roles.length > 0) {
      if (!item.roles.some((r) => userRoles.includes(r))) return false;
    }
    if (item.children && item.children.length > 0) {
      const hasVisibleChild = item.children.some(
        (c) => !c.roles || c.roles.some((r) => userRoles.includes(r))
      );
      if (!hasVisibleChild) return false;
    }
    return true;
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
            <div className="nav-section-label">CHỨC NĂNG HỆ THỐNG</div>
            {allowedNav.map((item) => {
              const Icon = item.icon;
              const visibleChildren = item.children
                ? item.children.filter((c) => !c.roles || c.roles.some((r) => userRoles.includes(r)))
                : [];

              const hasChildren = visibleChildren.length > 0;

              if (hasChildren) {
                const isExpanded = !!expandedMenus[item.id];
                const isAnyChildActive = visibleChildren.some((c) => isChildActive(c.path));

                return (
                  <div key={item.id} className="nav-group">
                    <button
                      type="button"
                      onClick={() => setExpandedMenus((prev) => ({ ...prev, [item.id]: !prev[item.id] }))}
                      className={`nav-parent-btn ${isAnyChildActive ? 'nav-parent-btn--active' : ''} ${isExpanded ? 'nav-parent-btn--open' : ''}`}
                      aria-expanded={isExpanded}
                    >
                      <span className="nav-link__icon">
                        <Icon size={18} />
                      </span>
                      <span className="nav-link__label">{item.label}</span>
                      {item.badge && <span className="badge badge--pink nav-badge">{item.badge}</span>}
                      <ChevronRight
                        size={16}
                        className={`nav-chevron-toggle ${isExpanded ? 'nav-chevron-toggle--rotated' : ''}`}
                      />
                    </button>

                    {isExpanded && (
                      <div className="nav-submenu">
                        {visibleChildren.map((child) => {
                          const ChildIcon = child.icon;
                          const isActive = isChildActive(child.path);
                          return (
                            <Link
                              key={child.id}
                              to={child.path}
                              className={`nav-sublink ${isActive ? 'nav-sublink--active' : ''}`}
                            >
                              {ChildIcon ? <ChildIcon size={14} /> : <span className="nav-sublink__dot" />}
                              <span style={{ flex: 1 }}>{child.label}</span>
                              {child.badge && <span className="badge badge--pink nav-badge">{child.badge}</span>}
                            </Link>
                          );
                        })}
                      </div>
                    )}
                  </div>
                );
              }

              // Standalone item (e.g. Dashboard)
              const isActive =
                item.path === '/dashboard'
                  ? location.pathname === '/dashboard'
                  : location.pathname.startsWith(item.path ?? '');

              return (
                <Link
                  key={item.id}
                  to={item.path ?? '#'}
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
            <div
              className="user-card card"
              onClick={() => navigate('/profile')}
              style={{ cursor: 'pointer' }}
              title="Xem và chỉnh sửa hồ sơ cá nhân"
            >
              <div className="user-avatar" aria-hidden="true">
                {currentUser?.fullName?.[0]?.toUpperCase() ?? 'U'}
              </div>
              <div className="user-info">
                <div className="user-name" title={currentUser?.fullName}>
                  {currentUser?.fullName ?? 'Người dùng'}
                </div>
                <div className="user-role-badge">
                  <span className="badge badge--mint" style={{ fontSize: '0.7rem', padding: '1px 6px' }}>
                    {userRoles[0] ?? 'User'}
                  </span>
                </div>
              </div>
            </div>

            <div className="sidebar-actions">
              <button
                type="button"
                className="btn btn--secondary btn--sm"
                onClick={() => navigate('/profile')}
                title="Quản lý hồ sơ cá nhân"
              >
                <User size={14} />
                <span>Hồ sơ cá nhân</span>
              </button>
              <button
                type="button"
                className="btn btn--secondary btn--sm btn--logout"
                onClick={handleLogout}
              >
                <LogOut size={14} />
                <span>Đăng xuất</span>
              </button>
            </div>
          </div>
        </aside>

        {/* MAIN WRAPPER */}
        <div className="layout-main-wrapper">
          {/* TOP HEADER */}
          <header className="layout-header">
            <div className="header-left">
              <span className="badge badge--violet header-pill">
                Hệ thống Quản trị & Bán trú Tiểu học
              </span>
              <ChildSelectorPill />
            </div>

            <div className="header-right">
              {/* Notifications dropdown trigger */}
              <div style={{ position: 'relative' }} ref={notifRef}>
                <button
                  type="button"
                  className="header-btn"
                  aria-label="Thông báo"
                  onClick={() => setShowNotifs((v) => !v)}
                >
                  <Bell size={18} />
                  {unreadCount > 0 && <span className="header-badge-dot" />}
                </button>

                {showNotifs && (
                  <div
                    className="card"
                    style={{
                      position: 'absolute',
                      right: 0,
                      top: 'calc(100% + 8px)',
                      width: 360,
                      maxHeight: 440,
                      overflowY: 'auto',
                      zIndex: 50,
                      boxShadow: 'var(--shadow-card)',
                      padding: 0,
                    }}
                  >
                    <div
                      style={{
                        padding: '12px 16px',
                        borderBottom: 'var(--border)',
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                        background: 'var(--color-muted)',
                      }}
                    >
                      <strong style={{ fontSize: 'var(--fs-sm)' }}>Thông báo</strong>
                      <span className="badge badge--violet" style={{ fontSize: '0.7rem' }}>
                        {unreadCount} chưa đọc
                      </span>
                    </div>

                    {notifications.length === 0 ? (
                      <div
                        style={{
                          padding: 24,
                          textAlign: 'center',
                          color: 'var(--color-muted-fg)',
                          fontSize: 'var(--fs-sm)',
                        }}
                      >
                        Không có thông báo nào.
                      </div>
                    ) : (
                      <div style={{ display: 'flex', flexDirection: 'column' }}>
                        {notifications.map((n) => (
                          <button
                            key={n.id}
                            type="button"
                            onClick={() => handleOpenNotification(n)}
                            style={{
                              padding: '10px 14px',
                              textAlign: 'left',
                              border: 'none',
                              borderBottom: '1px solid var(--color-border)',
                              background: n.isRead ? 'transparent' : 'var(--color-accent-soft)',
                              cursor: 'pointer',
                              display: 'flex',
                              flexDirection: 'column',
                              gap: 2,
                              transition: 'background 120ms ease',
                            }}
                          >
                            <span
                              style={{
                                fontSize: 'var(--fs-xs)',
                                fontWeight: n.isRead ? 600 : 800,
                                color: 'var(--color-fg)',
                              }}
                            >
                              {n.title}
                            </span>
                            <span
                              style={{
                                fontSize: 'var(--fs-xs)',
                                color: 'var(--color-muted-fg)',
                                lineHeight: 1.3,
                              }}
                            >
                              {n.content}
                            </span>
                            <span
                              style={{
                                fontSize: '0.65rem',
                                color: 'var(--color-muted-fg)',
                                marginTop: 2,
                              }}
                            >
                              {new Date(n.createdAt).toLocaleTimeString('vi-VN', {
                                hour: '2-digit',
                                minute: '2-digit',
                                day: '2-digit',
                                month: '2-digit',
                              })}
                            </span>
                          </button>
                        ))}
                      </div>
                    )}
                  </div>
                )}
              </div>

              {/* User greeting */}
              <div
                className="header-user"
                onClick={() => navigate('/profile')}
                style={{ cursor: 'pointer' }}
                title="Xem và chỉnh sửa hồ sơ cá nhân"
              >
                <div className="header-avatar" aria-hidden="true">
                  {currentUser?.fullName?.[0]?.toUpperCase() ?? 'U'}
                </div>
                <div className="user-greeting">
                  Xin chào, <strong>{currentUser?.fullName ?? 'Người dùng'}</strong>
                </div>
              </div>
            </div>
          </header>

          {/* PAGE CONTENT */}
          <main className="layout-content">
            <Outlet />
          </main>
        </div>
      </div>
    </ParentChildProvider>
  );
}
