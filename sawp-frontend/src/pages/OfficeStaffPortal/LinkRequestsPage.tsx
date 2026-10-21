import { useEffect, useState } from 'react';
import {
  AlertCircle,
  CheckCircle2,
  Clock,
  HeartHandshake,
  Phone,
  RefreshCw,
  Sparkles,
  XCircle,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { guardianApi } from '../../services/implementations/guardianApi';
import type { ChildLinkRequestItemDto } from '../../types/guardian.types';

export default function LinkRequestsPage() {
  const [requests, setRequests] = useState<ChildLinkRequestItemDto[]>([]);
  const [selectedStatus, setSelectedStatus] = useState<string>('UnderReview');
  const [loading, setLoading] = useState(false);
  const [processingId, setProcessingId] = useState<number | null>(null);

  useEffect(() => {
    loadRequests();
  }, [selectedStatus]);

  const loadRequests = async () => {
    setLoading(true);
    try {
      const res = await guardianApi.getLinkRequests(selectedStatus || undefined);
      if (res.success && res.data) {
        setRequests(res.data);
      }
    } catch {
      toast.error('Lỗi khi tải danh sách yêu cầu liên kết.');
    } finally {
      setLoading(false);
    }
  };

  const handleVerify = async (id: number, isApproved: boolean) => {
    let rejectionReason: string | undefined = undefined;
    if (!isApproved) {
      const reason = window.prompt('Nhập lý do từ chối yêu cầu liên kết:');
      if (reason === null) return; // user cancelled prompt
      if (!reason.trim()) {
        toast.error('Vui lòng nhập lý do từ chối!');
        return;
      }
      rejectionReason = reason.trim();
    }

    setProcessingId(id);
    try {
      const res = await guardianApi.verifyLinkRequest(id, {
        isApproved,
        rejectionReason,
      });

      if (res.success) {
        toast.success(
          isApproved
            ? 'Đã duyệt yêu cầu liên kết học sinh thành công!'
            : 'Đã từ chối yêu cầu liên kết.'
        );
        loadRequests();
      } else {
        toast.error(res.message || 'Xử lý thất bại.');
      }
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'Lỗi khi duyệt yêu cầu liên kết.');
    } finally {
      setProcessingId(null);
    }
  };

  return (
    <div className="container pop-in">
      {/* Decorative Memphis elements */}
      <span className="decor decor--circle" style={{ top: -20, right: 30 }} aria-hidden="true" />
      <span className="decor decor--triangle" style={{ bottom: 30, left: 10 }} aria-hidden="true" />

      {/* Page Header */}
      <header className="page-header" style={{ marginBottom: 24 }}>
        <span className="badge badge--pink" style={{ marginBottom: 8 }}>
          <Sparkles size={14} /> Duyệt hồ sơ Phụ huynh (Workflow 1)
        </span>
        <h1 style={{ margin: '4px 0 8px' }}>
          Yêu cầu <span className="squiggle">liên kết con</span>
        </h1>
        <p style={{ color: 'var(--color-muted-fg)', margin: 0 }}>
          Xem và xét duyệt các yêu cầu phụ huynh xin liên kết quyền giám hộ với học sinh trên hệ thống.
        </p>
      </header>

      {/* Filter Tabs */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20, flexWrap: 'wrap', gap: 12 }}>
        <div className="pill-tabs">
          <button
            type="button"
            className={`pill-tab ${selectedStatus === 'UnderReview' ? 'pill-tab--active' : ''}`}
            onClick={() => setSelectedStatus('UnderReview')}
          >
            Chờ duyệt (UnderReview)
          </button>
          <button
            type="button"
            className={`pill-tab ${selectedStatus === 'Approved' ? 'pill-tab--active' : ''}`}
            onClick={() => setSelectedStatus('Approved')}
          >
            Đã duyệt (Approved)
          </button>
          <button
            type="button"
            className={`pill-tab ${selectedStatus === 'Rejected' ? 'pill-tab--active' : ''}`}
            onClick={() => setSelectedStatus('Rejected')}
          >
            Đã từ chối (Rejected)
          </button>
          <button
            type="button"
            className={`pill-tab ${selectedStatus === '' ? 'pill-tab--active' : ''}`}
            onClick={() => setSelectedStatus('')}
          >
            Tất cả
          </button>
        </div>

        <button
          type="button"
          className="btn btn--secondary btn--sm"
          onClick={loadRequests}
          title="Làm mới danh sách"
        >
          <RefreshCw size={15} /> Làm mới
        </button>
      </div>

      {/* List of Requests */}
      {loading ? (
        <div className="card" style={{ textAlign: 'center', padding: 48 }}>
          <RefreshCw size={32} className="spin" color="var(--color-accent)" style={{ margin: '0 auto 12px' }} />
          <div style={{ fontWeight: 700 }}>Đang tải danh sách yêu cầu...</div>
        </div>
      ) : requests.length === 0 ? (
        <div className="card empty-state" style={{ textAlign: 'center', padding: 48 }}>
          <HeartHandshake size={48} color="var(--color-muted-fg)" style={{ margin: '0 auto 12px' }} />
          <h3>Không có yêu cầu liên kết nào</h3>
          <p style={{ color: 'var(--color-muted-fg)', maxWidth: 460, margin: '0 auto' }}>
            Hiện tại không có yêu cầu nào trong danh mục này. Phụ huynh gửi yêu cầu từ Cổng Phụ huynh sẽ xuất hiện tại đây.
          </p>
        </div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          {requests.map((req) => (
            <div
              key={req.requestId}
              className="card card--hover"
              style={{
                display: 'flex',
                flexDirection: 'column',
                gap: 14,
                borderColor: req.status === 'Approved' ? 'var(--color-quaternary)' : undefined,
              }}
            >
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 12 }}>
                {/* Parent & Student summary */}
                <div style={{ display: 'flex', gap: 14, alignItems: 'center' }}>
                  <div
                    style={{
                      width: 48,
                      height: 48,
                      borderRadius: '50%',
                      background: 'var(--color-secondary-soft)',
                      border: 'var(--border)',
                      display: 'grid',
                      placeItems: 'center',
                    }}
                  >
                    <HeartHandshake size={24} color="var(--color-secondary)" />
                  </div>
                  <div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <strong style={{ fontSize: 'var(--fs-base)' }}>
                        Phụ huynh: {req.parentFullName}
                      </strong>
                      <span className="badge badge--pink badge--sm">{req.relationship}</span>
                    </div>
                    <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', marginTop: 2, display: 'flex', gap: 12 }}>
                      <span>
                        <Phone size={12} style={{ verticalAlign: -1 }} /> {req.parentPhone || 'Chưa có SĐT'}
                      </span>
                      <span>
                        <Clock size={12} style={{ verticalAlign: -1 }} /> Ngày gửi:{' '}
                        {new Date(req.requestDate).toLocaleDateString('vi-VN')}
                      </span>
                    </div>
                  </div>
                </div>

                {/* Status & Auto-match badge */}
                <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                  {req.isAutoMatched ? (
                    <span className="badge badge--mint" title="Khớp tự động thông tin ngày sinh & SĐT phụ huynh trong hồ sơ gốc">
                      <CheckCircle2 size={12} /> Khớp tự động
                    </span>
                  ) : (
                    <span className="badge badge--amber" title="Thông tin cần giáo vụ xác minh đối chiếu hồ sơ giấy">
                      <AlertCircle size={12} /> Cần đối chiếu
                    </span>
                  )}

                  <span
                    className={`badge ${
                      req.status === 'Approved'
                        ? 'badge--mint'
                        : req.status === 'Rejected'
                        ? 'badge--pink'
                        : 'badge--amber'
                    }`}
                  >
                    {req.status === 'Approved'
                      ? 'Đã duyệt'
                      : req.status === 'Rejected'
                      ? 'Đã từ chối'
                      : 'Chờ duyệt'}
                  </span>
                </div>
              </div>

              {/* Student Target Box */}
              <div
                style={{
                  background: 'var(--color-bg)',
                  padding: '12px 16px',
                  borderRadius: 'var(--radius-sm)',
                  border: '1px solid var(--color-border)',
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  flexWrap: 'wrap',
                  gap: 8,
                }}
              >
                <div>
                  <div style={{ fontSize: 'var(--fs-xs)', textTransform: 'uppercase', color: 'var(--color-muted-fg)', fontWeight: 700 }}>
                    Học sinh xin liên kết
                  </div>
                  <div style={{ fontSize: 'var(--fs-base)', fontWeight: 700, marginTop: 2 }}>
                    {req.studentFullName}{' '}
                    <span className="badge badge--violet" style={{ marginLeft: 6 }}>
                      Mã: {req.studentCode}
                    </span>
                  </div>
                  <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', marginTop: 2 }}>
                    Ngày sinh khai báo: {new Date(req.studentDateOfBirth).toLocaleDateString('vi-VN')}
                  </div>
                </div>

                {req.systemCheckNote && (
                  <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-accent)', fontWeight: 600 }}>
                    ℹ️ {req.systemCheckNote}
                  </div>
                )}
              </div>

              {/* Rejection reason if rejected */}
              {req.rejectionReason && (
                <div className="banner banner--danger" style={{ fontSize: 'var(--fs-xs)', padding: 10 }}>
                  <strong>Lý do từ chối:</strong> {req.rejectionReason}
                </div>
              )}

              {/* Action Buttons for UnderReview status */}
              {req.status === 'UnderReview' && (
                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 4 }}>
                  <button
                    type="button"
                    className="btn btn--secondary btn--sm"
                    style={{ color: 'var(--color-secondary)' }}
                    onClick={() => handleVerify(req.requestId, false)}
                    disabled={processingId === req.requestId}
                  >
                    <XCircle size={15} /> Từ chối
                  </button>
                  <button
                    type="button"
                    className="btn btn--primary btn--sm"
                    onClick={() => handleVerify(req.requestId, true)}
                    disabled={processingId === req.requestId}
                  >
                    <CheckCircle2 size={15} /> Phê duyệt liên kết
                  </button>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
