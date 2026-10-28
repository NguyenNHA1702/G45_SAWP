import { Link } from 'react-router';
import {
  CalendarCheck2,
  CheckCircle2,
  ClipboardList,
  GraduationCap,
  HeartHandshake,
  Sparkles,
  Users,
  UtensilsCrossed,
} from 'lucide-react';
import { USER_KEY } from '../../services/apiClient';

export default function DashboardPage() {
  const userJson = localStorage.getItem(USER_KEY);
  const currentUser = userJson ? JSON.parse(userJson) : null;
  const roles: string[] = currentUser?.roles ?? [];

  return (
    <div className="container pop-in">
      {/* Decorative Memphis circles */}
      <span className="decor decor--circle" style={{ top: -20, right: 40 }} aria-hidden="true" />
      <span className="decor decor--triangle" style={{ bottom: 40, left: 20 }} aria-hidden="true" />

      <header className="page-header" style={{ marginBottom: 28 }}>
        <span className="badge badge--violet" style={{ marginBottom: 8 }}>
          <Sparkles size={14} /> Tổng quan nền tảng
        </span>
        <h1>
          Chào mừng, <span className="squiggle">{currentUser?.fullName ?? 'Thầy/Cô'}</span>!
        </h1>
        <p style={{ color: 'var(--color-muted-fg)' }}>
          Hệ thống Quản lý Hành chính & Dịch vụ Bán trú SAWP. Chọn nhanh chức năng dưới đây để bắt đầu.
        </p>
      </header>

      {/* Grid Quick Actions */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))',
          gap: 20,
          marginBottom: 36,
        }}
      >
        {roles.includes('Teacher') && (
          <Link
            to="/teacher/roster"
            className="card card--featured card--hover"
            style={{ textDecoration: 'none', color: 'inherit' }}
          >
            <div
              style={{
                width: 44,
                height: 44,
                borderRadius: '50%',
                background: 'var(--color-accent-soft)',
                border: 'var(--border)',
                display: 'grid',
                placeItems: 'center',
                marginBottom: 14,
              }}
            >
              <GraduationCap size={22} color="var(--color-accent)" />
            </div>
            <h3>Lớp chủ nhiệm (WF1)</h3>
            <p style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
              Xem danh sách chính thức học sinh của lớp, thông tin liên lạc phụ huynh và xuất danh sách.
            </p>
            <span className="badge badge--violet">Xem danh sách lớp →</span>
          </Link>
        )}

        {roles.includes('Teacher') && (
          <Link
            to="/teacher/attendance"
            className="card card--hover"
            style={{ textDecoration: 'none', color: 'inherit' }}
          >
            <div
              style={{
                width: 44,
                height: 44,
                borderRadius: '50%',
                background: 'var(--status-present-soft)',
                border: 'var(--border)',
                display: 'grid',
                placeItems: 'center',
                marginBottom: 14,
              }}
            >
              <CalendarCheck2 size={22} color="var(--color-fg)" />
            </div>
            <h3>Điểm danh hằng ngày</h3>
            <p style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
              Điểm danh lớp chủ nhiệm / tiết đầu, tự động trừ suất ăn và gửi cảnh báo vắng tới phụ huynh.
            </p>
            <span className="badge badge--mint">Mở trang điểm danh →</span>
          </Link>
        )}

        {roles.includes('Parent') && (
          <Link
            to="/parent/children"
            className="card card--featured card--hover"
            style={{ textDecoration: 'none', color: 'inherit' }}
          >
            <div
              style={{
                width: 44,
                height: 44,
                borderRadius: '50%',
                background: 'var(--color-secondary-soft)',
                border: 'var(--border)',
                display: 'grid',
                placeItems: 'center',
                marginBottom: 14,
              }}
            >
              <HeartHandshake size={22} color="var(--color-secondary)" />
            </div>
            <h3>Hồ sơ các con (WF1)</h3>
            <p style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
              Theo dõi kết quả xếp lớp, thông tin GVCN và gửi yêu cầu liên kết thêm con.
            </p>
            <span className="badge badge--pink">Xem hồ sơ con →</span>
          </Link>
        )}

        {(roles.includes('OfficeStaff') || roles.includes('Admin')) && (
          <>
            <Link
              to="/students"
              className="card card--featured card--hover"
              style={{ textDecoration: 'none', color: 'inherit' }}
            >
              <div
                style={{
                  width: 44,
                  height: 44,
                  borderRadius: '50%',
                  background: 'var(--color-accent-soft)',
                  border: 'var(--border)',
                  display: 'grid',
                  placeItems: 'center',
                  marginBottom: 14,
                }}
              >
                <Users size={22} color="var(--color-accent)" />
              </div>
              <h3>Hồ sơ học sinh (WF1)</h3>
              <p style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
                Quản lý hồ sơ định danh, nhập Excel hàng loạt, xử lý trùng lặp và kích hoạt hồ sơ.
              </p>
              <span className="badge badge--violet">Quản lý hồ sơ →</span>
            </Link>

            <Link
              to="/classes"
              className="card card--hover"
              style={{ textDecoration: 'none', color: 'inherit' }}
            >
              <div
                style={{
                  width: 44,
                  height: 44,
                  borderRadius: '50%',
                  background: 'var(--color-quaternary-soft)',
                  border: 'var(--border)',
                  display: 'grid',
                  placeItems: 'center',
                  marginBottom: 14,
                }}
              >
                <GraduationCap size={22} color="var(--color-quaternary)" />
              </div>
              <h3>Lớp học & Phân công</h3>
              <p style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
                Khởi tạo lớp hàng loạt, phân công GVCN (BR-16) và xếp lớp học sinh (BR-15, BR-18).
              </p>
              <span className="badge badge--mint">Quản lý lớp học →</span>
            </Link>

            <Link
              to="/link-requests"
              className="card card--hover"
              style={{ textDecoration: 'none', color: 'inherit' }}
            >
              <div
                style={{
                  width: 44,
                  height: 44,
                  borderRadius: '50%',
                  background: 'var(--color-secondary-soft)',
                  border: 'var(--border)',
                  display: 'grid',
                  placeItems: 'center',
                  marginBottom: 14,
                }}
              >
                <HeartHandshake size={22} color="var(--color-secondary)" />
              </div>
              <h3>Duyệt liên kết PH (WF1)</h3>
              <p style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
                Xét duyệt và phê duyệt các yêu cầu xin liên kết con của phụ huynh trên cổng trực tuyến.
              </p>
              <span className="badge badge--pink">Duyệt yêu cầu →</span>
            </Link>
          </>
        )}

        {roles.includes('Parent') && (
          <Link
            to="/parent/lunch"
            className="card card--hover"
            style={{ textDecoration: 'none', color: 'inherit' }}
          >
            <div
              style={{
                width: 44,
                height: 44,
                borderRadius: '50%',
                background: 'var(--color-tertiary-soft)',
                border: 'var(--border)',
                display: 'grid',
                placeItems: 'center',
                marginBottom: 14,
              }}
            >
              <UtensilsCrossed size={22} color="var(--color-fg)" />
            </div>
            <h3>Đăng ký & Lịch bán trú</h3>
            <p style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
              Chọn ngày đăng ký bán trú (UC-29), xem thực đơn & trạng thái từng ngày (UC-31) hoặc hủy trước 08h15 (UC-30).
            </p>
            <span className="badge badge--amber">Mở lịch bán trú →</span>
          </Link>
        )}

        {roles.includes('Parent') && (
          <Link
            to="/parent/attendance"
            className="card card--hover"
            style={{ textDecoration: 'none', color: 'inherit' }}
          >
            <div
              style={{
                width: 44,
                height: 44,
                borderRadius: '50%',
                background: 'var(--status-unexcused-soft)',
                border: 'var(--border)',
                display: 'grid',
                placeItems: 'center',
                marginBottom: 14,
              }}
            >
              <ClipboardList size={22} color="var(--color-fg)" />
            </div>
            <h3>Chuyên cần của con</h3>
            <p style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
              Xem lịch sử có mặt, đi muộn, vắng học và trạng thái suất ăn trưa từng ngày của con.
            </p>
            <span className="badge badge--pink">Xem chuyên cần →</span>
          </Link>
        )}

        {(roles.includes('ServiceStaff') || roles.includes('SchoolBoard') || roles.includes('OfficeStaff')) && (
          <Link
            to="/service/headcount"
            className="card card--hover"
            style={{ textDecoration: 'none', color: 'inherit' }}
          >
            <div
              style={{
                width: 44,
                height: 44,
                borderRadius: '50%',
                background: 'var(--color-tertiary-soft)',
                border: 'var(--border)',
                display: 'grid',
                placeItems: 'center',
                marginBottom: 14,
              }}
            >
              <UtensilsCrossed size={22} color="var(--color-fg)" />
            </div>
            <h3>Vận hành & Báo cáo Bán trú</h3>
            <p style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
              Cấu hình ngày phục vụ & thực đơn (UC-32), tổng hợp suất ăn theo lớp, chốt số liệu 08h15 và xuất danh sách (UC-33).
            </p>
            <span className="badge badge--amber">Xem thống kê bếp →</span>
          </Link>
        )}
      </div>

      {/* Thông tin hệ thống */}
      <div className="card" style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
        <CheckCircle2 size={32} color="var(--color-quaternary)" />
        <div>
          <h4 style={{ margin: 0, fontSize: 'var(--fs-base)' }}>Tài khoản đã xác thực thành công</h4>
          <p style={{ margin: 0, fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
            Phiên làm việc bảo mật với JWT Bearer Token. Menu bên trái hiển thị các chức năng theo đúng vai trò được phân công.
          </p>
        </div>
      </div>
    </div>
  );
}
