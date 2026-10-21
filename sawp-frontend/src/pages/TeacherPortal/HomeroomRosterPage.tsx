import { useEffect, useState } from 'react';
import { Link } from 'react-router';
import {
  CalendarCheck2,
  CheckCircle2,
  GraduationCap,
  Phone,
  Printer,
  RefreshCw,
  Sparkles,
  Users,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { classApi } from '../../services/implementations/classApi';
import type { ClassRosterDto } from '../../types/class.types';

export default function HomeroomRosterPage() {
  const [roster, setRoster] = useState<ClassRosterDto | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    loadMyClass();
  }, []);

  const loadMyClass = async () => {
    setLoading(true);
    try {
      const res = await classApi.getMyHomeroomClass();
      if (res.success && res.data) {
        setRoster(res.data);
      }
    } catch (err: any) {
      if (err.response?.status === 404) {
        setRoster(null);
      } else {
        toast.error('Lỗi khi tải danh sách lớp chủ nhiệm.');
      }
    } finally {
      setLoading(false);
    }
  };

  const handlePrint = () => {
    window.print();
  };

  const maleCount = roster?.students.filter((s) => s.gender === 'Nam').length ?? 0;
  const femaleCount = roster?.students.filter((s) => s.gender === 'Nữ').length ?? 0;

  return (
    <div className="container pop-in">
      {/* Decorative Memphis elements */}
      <span className="decor decor--circle" style={{ top: -20, right: 30 }} aria-hidden="true" />
      <span className="decor decor--triangle" style={{ bottom: 30, left: 10 }} aria-hidden="true" />

      {/* Page Header */}
      <header className="page-header" style={{ marginBottom: 24 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 16 }}>
          <div>
            <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 6 }}>
              <span className="badge badge--violet">
                <Sparkles size={14} /> Cổng Giáo Viên (Workflow 1)
              </span>
              {roster && (
                <span className="badge badge--amber">
                  Năm học {roster.schoolYearName}
                </span>
              )}
            </div>
            <h1 style={{ margin: '4px 0 8px' }}>
              Danh sách <span className="squiggle">lớp chủ nhiệm</span>
            </h1>
            <p style={{ color: 'var(--color-muted-fg)', margin: 0 }}>
              Xem danh sách chính thức học sinh của lớp, thông tin liên lạc phụ huynh và hồ sơ chuyên cần.
            </p>
          </div>

          {roster && (
            <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
              <button
                type="button"
                className="btn btn--secondary btn--sm"
                onClick={handlePrint}
              >
                <Printer size={16} /> In danh sách
              </button>
              <Link to="/teacher/attendance" className="btn btn--primary btn--sm" style={{ textDecoration: 'none' }}>
                <CalendarCheck2 size={16} /> Điểm danh ngay
              </Link>
            </div>
          )}
        </div>
      </header>

      {loading ? (
        <div className="card" style={{ textAlign: 'center', padding: 48 }}>
          <RefreshCw size={32} className="spin" color="var(--color-accent)" style={{ margin: '0 auto 12px' }} />
          <div style={{ fontWeight: 700 }}>Đang tải danh sách lớp của bạn...</div>
        </div>
      ) : !roster ? (
        <div className="card empty-state" style={{ textAlign: 'center', padding: 48 }}>
          <GraduationCap size={48} color="var(--color-muted-fg)" style={{ margin: '0 auto 12px' }} />
          <h3>Bạn chưa được phân công làm Chủ nhiệm lớp nào</h3>
          <p style={{ color: 'var(--color-muted-fg)', maxWidth: 460, margin: '0 auto 16px' }}>
            Theo quy tắc BR-07 & BR-20, giáo viên chỉ có quyền truy xuất danh sách của lớp mà mình được phân công chủ nhiệm. Vui lòng liên hệ Ban Giáo vụ để được phân công lớp.
          </p>
          <button type="button" className="btn btn--secondary btn--sm" onClick={loadMyClass}>
            <RefreshCw size={15} /> Kiểm tra lại
          </button>
        </div>
      ) : (
        <>
          {/* Top Class Stats */}
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
              gap: 16,
              marginBottom: 24,
            }}
          >
            <div className="stat-card">
              <div className="stat-icon" style={{ background: 'var(--color-accent-soft)' }}>
                <GraduationCap size={22} color="var(--color-accent)" strokeWidth={2.5} />
              </div>
              <div>
                <div style={{ fontSize: 'var(--fs-2xl)', fontFamily: 'var(--font-heading)', fontWeight: 800 }}>
                  Lớp {roster.className}
                </div>
                <div style={{ fontSize: 'var(--fs-xs)', textTransform: 'uppercase', color: 'var(--color-muted-fg)', fontWeight: 700 }}>
                  Khối {roster.grade} • Sức chứa {roster.capacity}
                </div>
              </div>
            </div>

            <div className="stat-card">
              <div className="stat-icon" style={{ background: 'var(--color-quaternary-soft)' }}>
                <Users size={22} color="var(--color-quaternary)" strokeWidth={2.5} />
              </div>
              <div>
                <div style={{ fontSize: 'var(--fs-2xl)', fontFamily: 'var(--font-heading)', fontWeight: 800 }}>
                  {roster.totalStudents} học sinh
                </div>
                <div style={{ fontSize: 'var(--fs-xs)', textTransform: 'uppercase', color: 'var(--color-muted-fg)', fontWeight: 700 }}>
                  {maleCount} Nam • {femaleCount} Nữ
                </div>
              </div>
            </div>

            <div className="stat-card">
              <div className="stat-icon" style={{ background: 'var(--color-secondary-soft)' }}>
                <CheckCircle2 size={22} color="var(--color-secondary)" strokeWidth={2.5} />
              </div>
              <div>
                <div style={{ fontSize: 'var(--fs-base)', fontFamily: 'var(--font-heading)', fontWeight: 800 }}>
                  {roster.isPlacementPublished ? 'Đã công bố chính thức' : 'Chưa công bố chính thức'}
                </div>
                <div style={{ fontSize: 'var(--fs-xs)', textTransform: 'uppercase', color: 'var(--color-muted-fg)', fontWeight: 700 }}>
                  Trạng thái xếp lớp
                </div>
              </div>
            </div>
          </div>

          {/* Official Roster Data Table */}
          <div className="data-table-wrapper">
            <table className="data-table">
              <thead>
                <tr>
                  <th style={{ width: 40 }}>STT</th>
                  <th>Mã học sinh</th>
                  <th>Họ và tên</th>
                  <th>Ngày sinh</th>
                  <th>Giới tính</th>
                  <th>Phụ huynh liên hệ</th>
                  <th>SĐT Phụ huynh</th>
                  <th>SĐT Khẩn cấp</th>
                  <th>Trạng thái</th>
                </tr>
              </thead>
              <tbody>
                {roster.students.map((s, idx) => (
                  <tr key={s.studentId}>
                    <td>{idx + 1}</td>
                    <td>
                      <span className="badge badge--violet" style={{ fontWeight: 700 }}>
                        {s.studentCode}
                      </span>
                    </td>
                    <td>
                      <strong style={{ fontSize: 'var(--fs-base)' }}>{s.fullName}</strong>
                    </td>
                    <td>{new Date(s.dateOfBirth).toLocaleDateString('vi-VN')}</td>
                    <td>{s.gender}</td>
                    <td>{s.primaryGuardianName || 'Chưa cập nhật'}</td>
                    <td>
                      {s.primaryGuardianPhone ? (
                        <a
                          href={`tel:${s.primaryGuardianPhone}`}
                          style={{ color: 'var(--color-accent)', textDecoration: 'none', fontWeight: 600, display: 'inline-flex', alignItems: 'center', gap: 4 }}
                        >
                          <Phone size={13} /> {s.primaryGuardianPhone}
                        </a>
                      ) : (
                        '-'
                      )}
                    </td>
                    <td>{s.emergencyContactPhone || '-'}</td>
                    <td>
                      <span className="badge badge--mint">{s.status}</span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  );
}
