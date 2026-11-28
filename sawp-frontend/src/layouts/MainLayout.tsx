import { useEffect, useState, useRef } from 'react';
import { createPortal } from 'react-dom';
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
  Menu,
  Newspaper,
  School,
  Sparkles,
  User,
  Users,
  UtensilsCrossed,
  ShieldCheck,
  FileSpreadsheet,
  Clock,
  Settings,
  UserCog,
  ShieldAlert,
  UserPlus,
  X,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { clearAuthSession, getAuthUser } from '../services/apiClient';
import { notificationApi } from '../services/implementations/notificationApi';
import { SYSTEM_MESSAGES } from '../constants/messages';
import { ParentChildProvider } from '../contexts/ParentChildContext';
import { ChildSelectorPill } from '../components/parent/ChildSelectorPill';
import { ensureUtcSuffix } from '../utils/date';
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
  // --- Bếp ăn bán trú & Báo cáo (ServiceStaff vận hành; SchoolBoard xem) ---
  {
    id: 'kitchen-reports',
    label: 'Báo cáo suất ăn Bếp',
    icon: UtensilsCrossed,
    roles: ['ServiceStaff', 'SchoolBoard'],
    children: [
      {
        id: 'kitchen-summary',
        label: 'Tổng hợp & Chốt suất',
        path: '/service/headcount?tab=summary',
        icon: FileSpreadsheet,
      },
      {
        id: 'kitchen-students',
        label: 'Danh sách nhận suất',
        path: '/service/headcount?tab=students',
        icon: Users,
      },
      {
        id: 'kitchen-logs',
        label: 'Nhật ký biến động',
        path: '/service/headcount?tab=logs',
        icon: Clock,
      },
      {
        id: 'kitchen-report',
        label: 'Báo cáo thống kê',
        path: '/service/headcount?tab=report',
        icon: ClipboardList,
      },
    ],
  },
  // --- Giáo vụ & Văn phòng (OfficeStaff) ---
  {
    id: 'office-students-group',
    label: 'Giáo vụ & Văn phòng',
    icon: Users,
    roles: ['OfficeStaff'],
    children: [
      {
        id: 'office-students',
        label: 'Hồ sơ học sinh',
        path: '/students',
        icon: Users,
      },
      {
        id: 'office-classes',
        label: 'Lớp học & Xếp lớp',
        path: '/classes',
        icon: GraduationCap,
      },
      {
        id: 'office-account-requests',
        label: 'Yêu cầu cấp tài khoản',
        path: '/office/account-requests',
        icon: UserPlus,
      },
      {
        id: 'office-requests',
        label: 'Xử lý đơn hành chính',
        path: '/office/requests',
        icon: FileCheck,
      },
      {
        id: 'office-attendance',
        label: 'Theo dõi chuyên cần',
        path: '/teacher/attendance',
        icon: CalendarCheck2,
      },
      {
        id: 'office-calendar',
        label: 'Lịch năm học & Học bù',
        path: '/admin/config',
        icon: CalendarCheck2,
      },
      {
        id: 'office-audit-logs',
        label: 'Nhật ký hệ thống',
        path: '/system/audit-logs',
        icon: ShieldAlert,
      },
    ],
  },
  // --- Ban giám hiệu (SchoolBoard) ---
  {
    id: 'board-group',
    label: 'Ban giám hiệu',
    icon: ShieldCheck,
    roles: ['SchoolBoard'],
    children: [
      {
        id: 'board-classes',
        label: 'Lớp học & Xếp lớp',
        path: '/classes',
        icon: GraduationCap,
      },
      {
        id: 'board-requests',
        label: 'Phê duyệt đơn cấp trường',
        path: '/board/requests',
        icon: ShieldCheck,
      },
      {
        id: 'board-leave',
        label: 'Duyệt đơn xin nghỉ',
        path: '/teacher/leave-requests',
        icon: FileCheck,
      },
      {
        id: 'board-attendance',
        label: 'Theo dõi chuyên cần',
        path: '/teacher/attendance',
        icon: CalendarCheck2,
      },
      {
        id: 'board-staff',
        label: 'Danh sách nhân sự',
        path: '/staff',
        icon: Users,
      },
      {
        id: 'board-audit-logs',
        label: 'Nhật ký hệ thống',
        path: '/system/audit-logs',
        icon: ShieldAlert,
      },
    ],
  },
  // --- Giáo viên chủ nhiệm (Teacher) ---
  {
    id: 'teacher-group',
    label: 'Giáo viên chủ nhiệm',
    icon: CalendarCheck2,
    roles: ['Teacher'],
    children: [
      {
        id: 'teacher-roster',
        label: 'Lớp chủ nhiệm',
        path: '/teacher/roster',
        icon: GraduationCap,
      },
      {
        id: 'teacher-boarding',
        label: 'Bán trú lớp học',
        path: '/teacher/boarding',
        icon: UtensilsCrossed,
      },
      {
        id: 'teacher-attendance',
        label: 'Điểm danh & Chuyên cần',
        path: '/teacher/attendance',
        icon: CalendarCheck2,
      },
      {
        id: 'teacher-leave',
        label: 'Duyệt đơn xin nghỉ',
        path: '/teacher/leave-requests',
        icon: FileCheck,
      },
      {
        id: 'teacher-contact-book',
        label: 'Sổ liên lạc lớp',
        path: '/teacher/contact-book',
        icon: Newspaper,
      },
    ],
  },
  // --- Cổng phụ huynh (Parent) ---
  {
    id: 'parent-group',
    label: 'Cổng phụ huynh',
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
        id: 'parent-contact-book',
        label: 'Sổ liên lạc điện tử',
        path: '/parent/contact-book',
        icon: Newspaper,
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
      {
        id: 'parent-requests',
        label: 'Gửi đơn & Giấy tờ',
        path: '/parent/requests',
        icon: FileCheck,
      },
    ],
  },
  // --- Quản trị hệ thống (Admin) ---
  {
    id: 'admin-system-group',
    label: 'Quản trị hệ thống',
    icon: Settings,
    roles: ['Admin'],
    children: [
      {
        id: 'admin-users',
        label: 'Quản lý tài khoản',
        path: '/admin/users',
        icon: UserCog,
      },
      {
        id: 'admin-staff',
        label: 'Danh sách nhân sự',
        path: '/staff',
        icon: Users,
      },
      {
        id: 'admin-account-requests',
        label: 'Duyệt yêu cầu cấp tài khoản',
        path: '/admin/account-requests',
        icon: UserPlus,
      },
      {
        id: 'admin-config',
        label: 'Cấu hình hệ thống',
        path: '/admin/config',
        icon: Settings,
      },
      {
        id: 'admin-audit-logs',
        label: 'Nhật ký hệ thống',
        path: '/system/audit-logs',
        icon: ShieldAlert,
      },
    ],
  },
];

export function MainLayout() {
  const navigate = useNavigate();
  const location = useLocation();
  const [notifications, setNotifications] = useState<AppNotification[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [showNotifs, setShowNotifs] = useState(false);
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const notifRef = useRef<HTMLDivElement>(null);

  // Tự đóng mobile drawer khi chuyển trang
  useEffect(() => {
    setIsMobileMenuOpen(false);
  }, [location.pathname, location.search]);

  // Expanded submenus state
  const [expandedMenus, setExpandedMenus] = useState<Record<string, boolean>>({
    'kitchen-reports': true,
  });

  // Lấy thông tin user hiện tại từ session/local storage và phản hồi khi cập nhật hồ sơ
  const [currentUser, setCurrentUser] = useState(() => {
    const userJson = getAuthUser();
    return userJson ? JSON.parse(userJson) : null;
  });

  useEffect(() => {
    const handleStorageChange = () => {
      const userJson = getAuthUser();
      setCurrentUser(userJson ? JSON.parse(userJson) : null);
    };
    window.addEventListener('storage', handleStorageChange);
    window.addEventListener('user-updated', handleStorageChange);
    return () => {
      window.removeEventListener('storage', handleStorageChange);
      window.removeEventListener('user-updated', handleStorageChange);
    };
  }, []);

  const rawRoles = currentUser?.roles ?? (currentUser?.role ? [currentUser.role] : []);
  const userRoles: string[] = Array.isArray(rawRoles) ? rawRoles : [String(rawRoles)];
  const hasRoleMatch = (rolesList?: string[]) =>
    !rolesList ||
    rolesList.some((r) =>
      userRoles.some((ur) => typeof ur === 'string' && ur.toLowerCase() === r.toLowerCase())
    );

  const isChildActive = (targetPathWithQuery: string) => {
    const [targetPath, targetQuery] = targetPathWithQuery.split('?');
    if (location.pathname !== targetPath) return false;

    if (targetQuery) {
      return location.search.includes(targetQuery);
    }

    return true;
  };

    // Tự động mở menu chứa trang hiện tại & đóng mobile drawer khi chuyển trang
  useEffect(() => {
    setIsMobileMenuOpen(false);
    NAV_ITEMS.forEach((item) => {
      if (item.children) {
        const hasActive = item.children.some((c) => isChildActive(c.path));
        if (hasActive) {
          setExpandedMenus((prev) => (prev[item.id] ? prev : { ...prev, [item.id]: true }));
        }
      }
    });
  }, [location.pathname, location.search]);

  // Khóa cuộn body & hỗ trợ phím Escape khi mở mobile drawer
  useEffect(() => {
    if (!isMobileMenuOpen) return;
    const prevOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setIsMobileMenuOpen(false);
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => {
      document.body.style.overflow = prevOverflow;
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [isMobileMenuOpen]);

  const fetchUnreadCount = async () => {
    try {
      const count = await notificationApi.getUnreadCount();
      setUnreadCount(count);
    } catch {
      // silent
    }
  };

  const fetchNotifications = async () => {
    try {
      const page = await notificationApi.getPaged(1, 20);
      setNotifications(page.items);
      setUnreadCount(page.items.filter((n) => !n.isRead).length);
    } catch {
      // silent
    }
  };

  useEffect(() => {
    fetchUnreadCount();
    const interval = setInterval(fetchUnreadCount, 15000);
    return () => clearInterval(interval);
  }, [location.pathname]);

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

  const handleOpenNotification = async (item: AppNotification) => {
    if (!item.isRead) {
      await notificationApi.markRead(item.id).catch(() => null);
      setNotifications((prev) => prev.map((n) => (n.id === item.id ? { ...n, isRead: true } : n)));
      setUnreadCount((c) => Math.max(0, c - 1));
    }
    setShowNotifs(false);
    if (item.linkUrl) {
      navigate(item.linkUrl);
    }
  };

  const handleLogout = () => {
    clearAuthSession();
    toast.success(SYSTEM_MESSAGES.MSG06.content);
    navigate('/login');
  };

  // Lọc menu theo roles
  const allowedNav = NAV_ITEMS.filter((item) => {
    if (item.roles && item.roles.length > 0) {
      if (!hasRoleMatch(item.roles)) return false;
    }
    if (item.children && item.children.length > 0) {
      const hasVisibleChild = item.children.some(
        (c) => !c.roles || hasRoleMatch(c.roles)
      );
      if (!hasVisibleChild) return false;
    }
    return true;
  });

  // Các mục điều hướng nhanh trên Bottom Nav (tối đa 4 mục + nút Menu)
  const mobileQuickNav = (() => {
    const base = [{ id: 'm-dashboard', label: 'Tổng quan', path: '/dashboard', icon: LayoutDashboard }];
    const has = (r: string) => userRoles.some((ur) => typeof ur === 'string' && ur.toLowerCase() === r.toLowerCase());

    if (has('Parent')) {
      return [
        ...base,
        { id: 'm-contact', label: 'Sổ liên lạc', path: '/parent/contact-book', icon: Newspaper },
        { id: 'm-lunch', label: 'Bán trú', path: '/parent/lunch', icon: UtensilsCrossed },
        { id: 'm-leave', label: 'Xin nghỉ', path: '/parent/leave-requests', icon: FileText },
      ];
    }
    if (has('Teacher')) {
      return [
        ...base,
        { id: 'm-attend', label: 'Điểm danh', path: '/teacher/attendance', icon: CalendarCheck2 },
        { id: 'm-boarding', label: 'Bán trú', path: '/teacher/boarding', icon: UtensilsCrossed },
        { id: 'm-leave', label: 'Duyệt đơn', path: '/teacher/leave-requests', icon: FileCheck },
      ];
    }
    if (has('ServiceStaff')) {
      return [
        ...base,
        { id: 'm-summary', label: 'Chốt suất', path: '/service/headcount?tab=summary', icon: FileSpreadsheet },
        { id: 'm-students', label: 'Nhận suất', path: '/service/headcount?tab=students', icon: Users },
        { id: 'm-report', label: 'Thống kê', path: '/service/headcount?tab=report', icon: ClipboardList },
      ];
    }
    if (has('OfficeStaff')) {
      return [
        ...base,
        { id: 'm-students', label: 'Học sinh', path: '/students', icon: Users },
        { id: 'm-classes', label: 'Lớp học', path: '/classes', icon: GraduationCap },
        { id: 'm-requests', label: 'Đơn từ', path: '/office/requests', icon: FileCheck },
      ];
    }
    if (has('SchoolBoard')) {
      return [
        ...base,
        { id: 'm-requests', label: 'Duyệt đơn', path: '/board/requests', icon: ShieldCheck },
        { id: 'm-attend', label: 'Chuyên cần', path: '/teacher/attendance', icon: CalendarCheck2 },
        { id: 'm-classes', label: 'Lớp học', path: '/classes', icon: GraduationCap },
      ];
    }
    if (has('Admin')) {
      return [
        ...base,
        { id: 'm-users', label: 'Tài khoản', path: '/admin/users', icon: UserCog },
        { id: 'm-acc-req', label: 'Cấp TK', path: '/admin/account-requests', icon: UserPlus },
        { id: 'm-config', label: 'Cấu hình', path: '/admin/config', icon: Settings },
      ];
    }
    return base;
  })();

  return (
    <ParentChildProvider userId={currentUser?.id} userRoles={userRoles}>
      <div className="layout-root">
        {/* MOBILE DRAWER BACKDROP */}
        {isMobileMenuOpen && (
          <div
            className="mobile-sidebar-backdrop"
            onClick={() => setIsMobileMenuOpen(false)}
            aria-hidden="true"
          />
        )}

        {/* SIDEBAR / MOBILE DRAWER */}
        <aside className={`layout-sidebar ${isMobileMenuOpen ? 'layout-sidebar--open' : ''}`}>
          <div className="sidebar-brand">
            <div className="brand-icon">
              <School size={24} color="#1E293B" />
            </div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div className="brand-title">
                SAWP <Sparkles size={14} className="brand-sparkle" />
              </div>
              <div className="brand-subtitle">Primary School</div>
            </div>
            <button
              type="button"
              className="mobile-drawer-close"
              onClick={() => setIsMobileMenuOpen(false)}
              aria-label="Đóng menu"
            >
              <X size={20} />
            </button>
          </div>

          <nav className="sidebar-nav">
            <div className="nav-section-label">CHỨC NĂNG HỆ THỐNG</div>
            {allowedNav.map((item) => {
              const Icon = item.icon;
              const visibleChildren = item.children
                ? item.children.filter((c) => !c.roles || hasRoleMatch(c.roles))
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
                <div className="user-role-badge" style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
                  {(userRoles.length > 0 ? userRoles : ['User']).map((role) => (
                    <span key={role} className="badge badge--mint" style={{ fontSize: '0.7rem', padding: '1px 6px' }}>
                      {role}
                    </span>
                  ))}
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
              <button
                type="button"
                className="mobile-menu-btn"
                onClick={() => setIsMobileMenuOpen(true)}
                aria-label="Mở menu điều hướng"
              >
                <Menu size={20} />
              </button>
              <span className="mobile-header-brand" onClick={() => navigate('/dashboard')}>
                SAWP
              </span>
              <span className="badge badge--violet header-pill">
                Hệ thống Quản trị & Bán trú Tiểu học
              </span>
              {userRoles.includes('Parent') && <ChildSelectorPill />}
            </div>

            <div className="header-right">
              {/* Notifications dropdown trigger */}
              <div style={{ position: 'relative' }} ref={notifRef}>
                <button
                  type="button"
                  className="header-btn"
                  aria-label={`Thông báo, ${unreadCount} chưa đọc`}
                  onClick={() => {
                    const next = !showNotifs;
                    setShowNotifs(next);
                    if (next) {
                      void fetchNotifications();
                    }
                  }}
                  style={{ position: 'relative' }}
                >
                  <Bell size={18} />
                  {unreadCount > 0 && (
                    <span
                      className="badge badge--pink"
                      style={{
                        position: 'absolute',
                        top: -5,
                        right: -5,
                        minWidth: 18,
                        height: 18,
                        borderRadius: 'var(--radius-full)',
                        padding: '0 4px',
                        fontSize: '0.65rem',
                        fontWeight: 900,
                        display: 'inline-flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        border: '1.5px solid var(--color-fg)',
                        background: 'var(--color-secondary)',
                        color: '#FFFFFF',
                        lineHeight: 1,
                      }}
                    >
                      {unreadCount > 99 ? '99+' : unreadCount}
                    </span>
                  )}
                </button>

                {showNotifs && (
                  <div className="card notif-dropdown">
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
                              {new Date(ensureUtcSuffix(n.createdAt)).toLocaleTimeString('vi-VN', {
                                timeZone: 'Asia/Ho_Chi_Minh',
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

      {/* MOBILE BOTTOM NAVIGATION (Portal to body so fixed positioning is never clipped) */}
      {typeof document !== 'undefined' &&
        createPortal(
          <nav className="mobile-bottom-nav" aria-label="Điều hướng nhanh">
            {mobileQuickNav.map((item) => {
              const Icon = item.icon;
              const active =
                item.path === '/dashboard'
                  ? location.pathname === '/dashboard'
                  : isChildActive(item.path);
              return (
                <Link
                  key={item.id}
                  to={item.path}
                  className={`mobile-bottom-nav__item ${active ? 'mobile-bottom-nav__item--active' : ''}`}
                >
                  <span className="mobile-bottom-nav__icon">
                    <Icon size={18} />
                  </span>
                  <span className="mobile-bottom-nav__label">{item.label}</span>
                </Link>
              );
            })}
            <button
              type="button"
              onClick={() => setIsMobileMenuOpen((prev) => !prev)}
              className={`mobile-bottom-nav__item ${isMobileMenuOpen ? 'mobile-bottom-nav__item--active' : ''}`}
              aria-label="Mở tất cả chức năng"
            >
              <span className="mobile-bottom-nav__icon">
                <Menu size={18} />
              </span>
              <span className="mobile-bottom-nav__label">Menu</span>
            </button>
          </nav>,
          document.body
        )}
    </ParentChildProvider>
  );
}
