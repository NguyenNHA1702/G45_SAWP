import { useEffect, useState } from 'react';
import { Link } from 'react-router';
import {
  CalendarCheck2,
  CheckCircle2,
  Clock,
  GraduationCap,
  HeartHandshake,
  Plus,
  RefreshCw,
  Sparkles,
  UtensilsCrossed,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { guardianApi } from '../../services/implementations/guardianApi';
import { LinkChildModal } from '../../components/parent/LinkChildModal';
import type { LinkedChildSummaryDto } from '../../types/guardian.types';

export default function ChildDashboardPage() {
  const [children, setChildren] = useState<LinkedChildSummaryDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [linkModalOpen, setLinkModalOpen] = useState(false);

  useEffect(() => {
    loadMyChildren();
  }, []);

  const loadMyChildren = async () => {
    setLoading(true);
    try {
      const res = await guardianApi.getMyChildren();
      if (res.success && res.data) {
        setChildren(res.data);
      }
    } catch {
      toast.error('Lỗi khi tải thông tin con của bạn.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="container pop-in">
      {/* Decorative Memphis shapes */}
      <span className="decor decor--circle" style={{ top: -20, right: 30 }} aria-hidden="true" />
      <span className="decor decor--triangle" style={{ bottom: 30, left: 10 }} aria-hidden="true" />

      {/* Page Header */}
      <header className="page-header" style={{ marginBottom: 24 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 16 }}>
          <div>
            <span className="badge badge--pink" style={{ marginBottom: 8 }}>
              <Sparkles size={14} /> Cổng Phụ Huynh
            </span>
            <h1 style={{ margin: '4px 0 8px' }}>
              Thông tin <span className="squiggle">các con</span>
            </h1>
            <p style={{ color: 'var(--color-muted-fg)', margin: 0 }}>
              Theo dõi kết quả xếp lớp, thông tin giáo viên chủ nhiệm và chuyên cần bán trú của con bạn.
            </p>
          </div>

          <button
            type="button"
            className="btn btn--primary"
            onClick={() => setLinkModalOpen(true)}
          >
            <Plus size={18} /> Liên kết thêm con
          </button>
        </div>
      </header>

      {/* List of Linked Children */}
      {loading ? (
        <div className="card" style={{ textAlign: 'center', padding: 48 }}>
          <RefreshCw size={32} className="spin" color="var(--color-accent)" style={{ margin: '0 auto 12px' }} />
          <div style={{ fontWeight: 700 }}>Đang tải thông tin...</div>
        </div>
      ) : children.length === 0 ? (
        <div className="card empty-state" style={{ textAlign: 'center', padding: 48 }}>
          <HeartHandshake size={48} color="var(--color-muted-fg)" style={{ margin: '0 auto 12px' }} />
          <h3>Bạn chưa liên kết với hồ sơ học sinh nào</h3>
          <p style={{ color: 'var(--color-muted-fg)', maxWidth: 460, margin: '0 auto 16px' }}>
            Để theo dõi kết quả xếp lớp và điểm danh của con, vui lòng nhấn nút dưới đây để gửi thông tin liên kết.
          </p>
          <button
            type="button"
            className="btn btn--primary btn--sm"
            onClick={() => setLinkModalOpen(true)}
          >
            <Plus size={16} /> Liên kết hồ sơ con ngay
          </button>
        </div>
      ) : (
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fill, minmax(360px, 1fr))',
            gap: 24,
          }}
        >
          {children.map((child) => (
            <div
              key={child.studentId}
              className="card card--featured card--hover"
              style={{
                display: 'flex',
                flexDirection: 'column',
                gap: 16,
                position: 'relative',
              }}
            >
              {/* Header: Avatar, Name, Code */}
              <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
                <div
                  style={{
                    width: 56,
                    height: 56,
                    borderRadius: '50%',
                    background: 'var(--color-secondary)',
                    color: '#ffffff',
                    border: 'var(--border)',
                    boxShadow: 'var(--shadow-pop-active)',
                    display: 'grid',
                    placeItems: 'center',
                    fontFamily: 'var(--font-heading)',
                    fontSize: 'var(--fs-lg)',
                    fontWeight: 800,
                  }}
                >
                  {child.fullName.charAt(0)}
                </div>
                <div style={{ flex: 1 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <h3 style={{ margin: 0 }}>{child.fullName}</h3>
                    <span className="badge badge--pink badge--sm">{child.relationshipName}</span>
                  </div>
                  <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', marginTop: 2 }}>
                    Mã định danh: <strong>{child.studentCode}</strong> • Ngày sinh:{' '}
                    {new Date(child.dateOfBirth).toLocaleDateString('vi-VN')}
                  </div>
                </div>
              </div>

              {/* Placement & Class Box */}
              <div
                style={{
                  background: 'var(--color-bg)',
                  padding: '14px 16px',
                  borderRadius: 'var(--radius-md)',
                  border: '1px solid var(--color-border)',
                }}
              >
                {child.isPlacementPublished && child.currentClassName ? (
                  <div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
                      <span className="badge badge--mint">
                        <CheckCircle2 size={12} /> Đã công bố kết quả xếp lớp
                      </span>
                      <span className="badge badge--violet">Khối {child.grade}</span>
                    </div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginTop: 6 }}>
                      <GraduationCap size={24} color="var(--color-accent)" />
                      <div>
                        <div style={{ fontSize: 'var(--fs-xs)', textTransform: 'uppercase', color: 'var(--color-muted-fg)', fontWeight: 700 }}>
                          Lớp học chính thức
                        </div>
                        <div style={{ fontSize: 'var(--fs-lg)', fontWeight: 800, fontFamily: 'var(--font-heading)', color: 'var(--color-accent)' }}>
                          Lớp {child.currentClassName}
                        </div>
                      </div>
                    </div>
                  </div>
                ) : (
                  <div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6, color: 'var(--color-tertiary)', fontWeight: 700, fontSize: 'var(--fs-sm)' }}>
                      <Clock size={16} /> Đang hoàn thiện danh sách lớp
                    </div>
                    <p style={{ margin: '6px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                      Ban Giáo vụ đang tiến hành xếp lớp cho năm học mới. Kết quả chính thức sẽ được công bố tại đây khi hoàn tất.
                    </p>
                  </div>
                )}
              </div>

              {/* Quick Action Shortcuts */}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10, marginTop: 'auto' }}>
                <Link
                  to="/parent/attendance"
                  className="btn btn--secondary btn--sm"
                  style={{ textDecoration: 'none' }}
                >
                  <CalendarCheck2 size={15} /> Xem chuyên cần
                </Link>
                <button
                  type="button"
                  className="btn btn--secondary btn--sm"
                  onClick={() => toast('Tính năng đăng ký suất ăn bán trú sắp mở')}
                >
                  <UtensilsCrossed size={15} /> Suất ăn bán trú
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Link Child Modal */}
      <LinkChildModal
        open={linkModalOpen}
        onClose={() => setLinkModalOpen(false)}
        onSubmitted={() => loadMyChildren()}
      />
    </div>
  );
}
