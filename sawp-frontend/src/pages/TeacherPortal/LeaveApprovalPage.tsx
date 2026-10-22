import { useEffect, useState } from 'react';
import {
  Check,
  X,
  ShieldAlert,
  CheckCircle2,
  UserCheck,
  MessageSquare,
  AlertTriangle,
  Send,
  FileText,
  Sparkles,
  UtensilsCrossed,
  Paperclip,
  ExternalLink,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { leaveRequestApi, type LeaveRequestItem } from '../../services/implementations/leaveRequestApi';
import { USER_KEY } from '../../services/apiClient';
import { LUNCH_CUTOFF_LABEL } from '../../constants/lunch';
import { GeometricModal } from '../../components/common/GeometricModal';

export default function LeaveApprovalPage() {
  const [requests, setRequests] = useState<LeaveRequestItem[]>([]);
  const [loading, setLoading] = useState(true);

  // User role detection
  const userJson = localStorage.getItem(USER_KEY);
  const currentUser = userJson ? JSON.parse(userJson) : null;
  const userRoles: string[] = currentUser?.roles ?? [];
  const isSchoolBoard = userRoles.includes('SchoolBoard');
  const isAdmin = userRoles.includes('Admin');

  // Status Filter: default 'UnderReview' for SchoolBoard, 'Submitted' for Teacher
  const [statusFilter, setStatusFilter] = useState<string>(isSchoolBoard ? 'UnderReview' : 'Submitted');

  // Modal Reject state
  const [rejectingItem, setRejectingItem] = useState<LeaveRequestItem | null>(null);
  const [rejectReason, setRejectReason] = useState('');

  // Modal Forward to School Board state
  const [forwardingItem, setForwardingItem] = useState<LeaveRequestItem | null>(null);
  const [forwardNotes, setForwardNotes] = useState('');

  const [processing, setProcessing] = useState(false);

  const fetchRequests = async () => {
    try {
      setLoading(true);
      const res = await leaveRequestApi.getClassRequests({
        status: statusFilter === 'All' ? undefined : statusFilter,
      });
      if (res.success) {
        setRequests(res.data);
      }
    } catch {
      toast.error('Không thể tải danh sách đơn xin nghỉ.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchRequests();
  }, [statusFilter]);

  // GVCN duyệt đơn thông thường (< 3 ngày)
  const handleApprove = async (item: LeaveRequestItem) => {
    if (!window.confirm(`Xác nhận PHÊ DUYỆT đơn xin nghỉ cho học sinh ${item.studentName}?`)) return;

    try {
      setProcessing(true);
      const res = await leaveRequestApi.review(item.id, {
        isApproved: true,
        reviewerNotes: 'GVCN đồng ý cho học sinh nghỉ học.',
      });

      if (res.success) {
        toast.success(`Đã duyệt đơn thành công! ${res.data.mealSyncMessage}`, { duration: 5000 });
        fetchRequests();
      } else {
        toast.error(res.message);
      }
    } catch (err: unknown) {
      const errorMsg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message || 'Có lỗi xảy ra.';
      toast.error(errorMsg);
    } finally {
      setProcessing(false);
    }
  };

  // Ban Giám Hiệu duyệt đơn chuyển cấp (>= 3 ngày)
  const handleBghApprove = async (item: LeaveRequestItem) => {
    if (!window.confirm(`Ban Giám Hiệu xác nhận PHÊ DUYỆT đơn xin nghỉ ${item.totalDays} ngày cho học sinh ${item.studentName}?`)) return;

    try {
      setProcessing(true);
      const res = await leaveRequestApi.review(item.id, {
        isApproved: true,
        reviewerNotes: 'Ban Giám Hiệu đồng ý phê duyệt đơn xin nghỉ học.',
      });

      if (res.success) {
        toast.success(`Ban Giám Hiệu đã phê duyệt đơn thành công! ${res.data.mealSyncMessage}`, { duration: 5000 });
        fetchRequests();
      } else {
        toast.error(res.message);
      }
    } catch (err: unknown) {
      const errorMsg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message || 'Có lỗi xảy ra.';
      toast.error(errorMsg);
    } finally {
      setProcessing(false);
    }
  };

  // Xác nhận từ chối đơn (GVCN hoặc BGH)
  const handleConfirmReject = async () => {
    if (!rejectingItem) return;
    if (!rejectReason.trim()) {
      toast.error('Bắt buộc phải nhập lý do từ chối.');
      return;
    }

    try {
      setProcessing(true);
      const res = await leaveRequestApi.review(rejectingItem.id, {
        isApproved: false,
        reviewerNotes: rejectReason.trim(),
      });

      if (res.success) {
        toast.success(
          isSchoolBoard
            ? 'Ban Giám Hiệu đã từ chối đơn và gửi thông báo đến phụ huynh.'
            : 'Giáo viên đã từ chối đơn và gửi thông báo đến phụ huynh.'
        );
        setRejectingItem(null);
        setRejectReason('');
        fetchRequests();
      } else {
        toast.error(res.message);
      }
    } catch (err: unknown) {
      const errorMsg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message || 'Có lỗi xảy ra.';
      toast.error(errorMsg);
    } finally {
      setProcessing(false);
    }
  };

  // GVCN chuyển đơn lên Ban Giám Hiệu (đơn >= 3 ngày)
  const handleConfirmForward = async () => {
    if (!forwardingItem) return;

    try {
      setProcessing(true);
      const res = await leaveRequestApi.review(forwardingItem.id, {
        isApproved: true,
        isForwardToSchoolBoard: true,
        reviewerNotes: forwardNotes.trim() || 'GVCN đã xác nhận thông tin, kính chuyển Ban Giám Hiệu xem xét phê duyệt.',
      });

      if (res.success) {
        toast.success('Đã xác nhận yêu cầu và chuyển lên Ban Giám Hiệu thành công!');
        setForwardingItem(null);
        setForwardNotes('');
        fetchRequests();
      } else {
        toast.error(res.message);
      }
    } catch (err: unknown) {
      const errorMsg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message || 'Có lỗi xảy ra.';
      toast.error(errorMsg);
    } finally {
      setProcessing(false);
    }
  };

  // Danh sách các tab theo vai trò
  const filterTabs = isSchoolBoard
    ? [
        { key: 'UnderReview', label: 'Chờ BGH duyệt' },
        { key: 'Approved', label: 'Đã phê duyệt' },
        { key: 'Rejected', label: 'Đã từ chối' },
        { key: 'All', label: 'Tất cả' },
      ]
    : [
        { key: 'Submitted', label: 'Chờ GVCN xử lý' },
        { key: 'UnderReview', label: 'Đã chuyển BGH' },
        { key: 'Approved', label: 'Đã phê duyệt' },
        { key: 'Rejected', label: 'Đã từ chối' },
        { key: 'All', label: 'Tất cả' },
      ];

  return (
    <div className="container pop-in">
      {/* Decorative Memphis elements */}
      <span className="decor decor--circle" style={{ top: -20, right: 30 }} aria-hidden="true" />
      <span className="decor decor--triangle" style={{ bottom: 30, left: 10 }} aria-hidden="true" />
      <span className="decor decor--square" style={{ top: 140, right: -15 }} aria-hidden="true" />

      {/* Page Header */}
      <header className="page-header" style={{ marginBottom: 24 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 16 }}>
          <div>
            <span className={`badge ${isSchoolBoard ? 'badge--violet' : 'badge--pink'}`} style={{ marginBottom: 8 }}>
              <Sparkles size={14} /> {isSchoolBoard ? 'Ban Giám Hiệu' : 'Cổng Giáo Viên'}
            </span>
            <h1 style={{ margin: '4px 0 8px' }}>
              Duyệt đơn xin <span className="squiggle">nghỉ học</span>
            </h1>
            <p style={{ color: 'var(--color-muted-fg)', margin: 0, fontSize: 'var(--fs-base)' }}>
              {isSchoolBoard
                ? 'Phê duyệt các đơn xin nghỉ từ 3 ngày trở lên do Giáo viên chủ nhiệm chuyển tiếp. Tự động đồng bộ điểm danh và suất ăn bán trú.'
                : 'Đơn dưới 3 ngày do GVCN duyệt trực tiếp. Đơn từ 3 ngày trở lên GVCN xác nhận và chuyển Ban Giám Hiệu phê duyệt.'}
            </p>
          </div>
        </div>
      </header>

      {/* Filter Tabs as Playful Pills */}
      <div style={{ display: 'flex', gap: 10, marginBottom: 24, flexWrap: 'wrap' }}>
        {filterTabs.map((tab) => {
          const isActive = statusFilter === tab.key;
          return (
            <button
              key={tab.key}
              type="button"
              className={isActive ? 'btn btn--primary btn--sm' : 'btn btn--secondary btn--sm'}
              onClick={() => setStatusFilter(tab.key)}
            >
              {tab.label}
            </button>
          );
        })}
      </div>

      {/* List */}
      {loading ? (
        <div className="card" style={{ textAlign: 'center', padding: '48px 24px' }}>
          <p style={{ color: 'var(--color-muted-fg)', fontSize: 'var(--fs-base)', margin: 0 }}>Đang tải danh sách đơn xin nghỉ...</p>
        </div>
      ) : requests.length === 0 ? (
        <div className="card empty-state">
          <UserCheck size={48} color="var(--color-muted-fg)" style={{ marginBottom: 12 }} />
          <h3 style={{ margin: 0, fontFamily: 'var(--font-heading)' }}>Không có đơn xin nghỉ nào trong mục này</h3>
          <p style={{ color: 'var(--color-muted-fg)', fontSize: 'var(--fs-sm)', marginTop: 6 }}>
            Các đơn mới từ phụ huynh sẽ tự động xuất hiện tại đây khi được gửi.
          </p>
        </div>
      ) : (
        <div style={{ display: 'grid', gap: 18 }}>
          {requests.map((r) => {
            const isEscalated = (r.totalDays || 1) >= 3;
            return (
              <div
                key={r.id}
                className="card card--hover"
                style={{
                  border: isEscalated ? '2px solid var(--color-accent)' : 'var(--border)',
                  boxShadow: isEscalated ? '8px 8px 0 0 var(--color-accent-soft)' : 'var(--shadow-card)',
                }}
              >
                {/* Header card */}
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 10, marginBottom: 12 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                    <span className="badge badge--muted" style={{ fontFamily: 'monospace', fontWeight: 700 }}>
                      {r.requestNumber}
                    </span>
                    <strong style={{ fontSize: 'var(--fs-md)', color: 'var(--color-fg)', fontFamily: 'var(--font-heading)' }}>
                      {r.studentName}
                    </strong>
                    <span style={{ color: 'var(--color-muted-fg)', fontSize: 'var(--fs-sm)' }}>
                      Mã: {r.studentCode}
                    </span>
                    {r.className && (
                      <span className="badge badge--mint">
                        Lớp {r.className}
                      </span>
                    )}
                  </div>

                  <div>
                    {r.status === 'Submitted' && (
                      <span className="badge badge--amber">
                        {isEscalated ? 'Chờ GVCN chuyển BGH' : 'Chờ GVCN duyệt'}
                      </span>
                    )}
                    {r.status === 'UnderReview' && (
                      <span className="badge badge--violet">
                        Chờ BGH duyệt
                      </span>
                    )}
                    {r.status === 'Approved' && (
                      <span className="badge badge--mint">
                        <CheckCircle2 size={14} /> Đã phê duyệt
                      </span>
                    )}
                    {r.status === 'Rejected' && (
                      <span className="badge badge--pink">
                        <ShieldAlert size={14} /> Đã từ chối
                      </span>
                    )}
                    {r.status === 'Cancelled' && (
                      <span className="badge badge--muted">
                        Đã hủy
                      </span>
                    )}
                  </div>
                </div>

                {/* Routing Banner on Card */}
                {r.status === 'Submitted' && isEscalated && (
                  <div
                    className="card"
                    style={{
                      background: 'var(--color-accent-soft)',
                      borderColor: 'var(--color-accent)',
                      padding: '10px 14px',
                      marginBottom: 12,
                      display: 'flex',
                      alignItems: 'center',
                      gap: 8,
                      fontSize: 'var(--fs-sm)',
                    }}
                  >
                    <ShieldAlert size={18} color="var(--color-accent)" style={{ flexShrink: 0 }} />
                    <span>
                      Đơn xin nghỉ <strong>{r.totalDays} ngày</strong> (từ 3 ngày trở lên) — GVCN cần xác nhận thông tin và chuyển tiếp lên Ban Giám Hiệu.
                    </span>
                  </div>
                )}

                {r.status === 'UnderReview' && (
                  <div
                    className="card"
                    style={{
                      background: 'var(--color-accent-soft)',
                      borderColor: 'var(--color-accent)',
                      padding: '10px 14px',
                      marginBottom: 12,
                      display: 'flex',
                      alignItems: 'center',
                      gap: 8,
                      fontSize: 'var(--fs-sm)',
                    }}
                  >
                    <FileText size={18} color="var(--color-accent)" style={{ flexShrink: 0 }} />
                    <span>
                      Đơn nghỉ chuyển cấp <strong>{r.totalDays} ngày</strong> đã được GVCN xác nhận và đang chờ Ban Giám Hiệu phê duyệt.
                    </span>
                  </div>
                )}

                {/* Information Grid */}
                <div
                  style={{
                    display: 'grid',
                    gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
                    gap: 12,
                    fontSize: 'var(--fs-sm)',
                    padding: '12px 16px',
                    background: 'var(--color-muted)',
                    borderRadius: 'var(--radius-sm)',
                    border: '1px solid var(--color-border)',
                  }}
                >
                  <div>
                    <span style={{ color: 'var(--color-muted-fg)' }}>Phụ huynh gửi:</span>{' '}
                    <strong>{r.requesterName}</strong> {r.requesterPhone && <small style={{ color: 'var(--color-muted-fg)' }}>({r.requesterPhone})</small>}
                  </div>
                  <div>
                    <span style={{ color: 'var(--color-muted-fg)' }}>Thời gian:</span>{' '}
                    <strong>
                      {new Date(r.startDate).toLocaleDateString('vi-VN')} {r.startDate !== r.endDate && `→ ${new Date(r.endDate).toLocaleDateString('vi-VN')}`}
                    </strong>{' '}
                    <span className={`badge ${isEscalated ? 'badge--violet' : 'badge--mint'}`} style={{ fontSize: 'var(--fs-xs)', marginLeft: 4 }}>
                      {r.totalDays || 1} ngày
                    </span>
                  </div>
                  <div>
                    <span style={{ color: 'var(--color-muted-fg)' }}>Buổi:</span> <strong>{r.sessionName}</strong>
                  </div>
                  <div>
                    <span style={{ color: 'var(--color-muted-fg)' }}>Bán trú:</span>{' '}
                    {r.keepLunchService ? (
                      <span style={{ color: 'var(--color-accent)', fontWeight: 700 }}>
                        <UtensilsCrossed size={14} style={{ display: 'inline', verticalAlign: 'middle', marginRight: 4 }} />
                        Phụ huynh chọn GIỮ SUẤT ĂN
                      </span>
                    ) : (
                      <span>Tự động hủy nếu duyệt trước {LUNCH_CUTOFF_LABEL}</span>
                    )}
                  </div>
                </div>

                {/* Reason & Attachments */}
                <div style={{ marginTop: 12, fontSize: 'var(--fs-sm)', lineHeight: 1.5 }}>
                  <p style={{ margin: 0 }}>
                    <strong style={{ color: 'var(--color-muted-fg)' }}>Lý do phụ huynh trình bày:</strong>{' '}
                    <span style={{ color: 'var(--color-fg)' }}>{r.reason}</span>
                  </p>
                  {r.doctorNoteAttachmentUrl && (
                    <div style={{ marginTop: 8 }}>
                      {r.doctorNoteAttachmentUrl.startsWith('/') || r.doctorNoteAttachmentUrl.startsWith('http') ? (
                        <a
                          href={r.doctorNoteAttachmentUrl}
                          target="_blank"
                          rel="noreferrer"
                          className="btn btn--secondary btn--sm"
                          style={{
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: 6,
                            textDecoration: 'none',
                            color: 'var(--color-accent)',
                            borderColor: 'var(--color-accent)',
                          }}
                        >
                          <Paperclip size={14} />
                          <span>Xem minh chứng của phụ huynh</span>
                          <ExternalLink size={12} />
                        </a>
                      ) : (
                        <div style={{ display: 'inline-flex', alignItems: 'center', gap: 6, color: 'var(--color-accent)' }}>
                          <Paperclip size={14} />
                          <strong>Minh chứng:</strong> {r.doctorNoteAttachmentUrl}
                        </div>
                      )}
                    </div>
                  )}
                  {r.reviewerNotes && (
                    <div
                      style={{
                        marginTop: 8,
                        paddingTop: 8,
                        borderTop: '1px dashed var(--color-border)',
                        color: r.status === 'Rejected' ? 'var(--color-secondary)' : 'var(--color-accent)',
                      }}
                    >
                      <strong>Nhật ký xử lý:</strong> {r.reviewerNotes}
                    </div>
                  )}
                </div>

                {/* ACTION BUTTONS */}
                {/* 1. Branch: Dưới 3 ngày (GVCN xem xét đơn nghỉ thông thường) */}
                {r.status === 'Submitted' && !isEscalated && (
                  <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, paddingTop: 12, borderTop: '1px solid var(--color-border)', marginTop: 8 }}>
                    <button
                      type="button"
                      disabled={processing}
                      className="btn btn--secondary btn--sm"
                      style={{ color: '#e11d48', borderColor: '#fda4af' }}
                      onClick={() => {
                        setRejectingItem(r);
                        setRejectReason('');
                      }}
                    >
                      <X size={15} /> Từ chối
                    </button>
                    <button
                      type="button"
                      disabled={processing}
                      className="btn btn--primary btn--sm"
                      style={{ background: 'var(--color-quaternary)', color: 'var(--color-fg)' }}
                      onClick={() => handleApprove(r)}
                    >
                      <Check size={15} /> Phê duyệt đơn
                    </button>
                  </div>
                )}

                {/* 2. Branch: Từ 3 ngày trở lên (GVCN xác nhận yêu cầu và chuyển lên Ban Giám hiệu) */}
                {r.status === 'Submitted' && isEscalated && (
                  <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, paddingTop: 12, borderTop: '1px solid var(--color-border)', marginTop: 8 }}>
                    <button
                      type="button"
                      disabled={processing}
                      className="btn btn--secondary btn--sm"
                      style={{ color: '#e11d48', borderColor: '#fda4af' }}
                      onClick={() => {
                        setRejectingItem(r);
                        setRejectReason('');
                      }}
                    >
                      <X size={15} /> Từ chối
                    </button>
                    <button
                      type="button"
                      disabled={processing}
                      className="btn btn--primary btn--sm"
                      onClick={() => {
                        setForwardingItem(r);
                        setForwardNotes(`Kính chuyển Ban Giám Hiệu xem xét phê duyệt đơn xin nghỉ ${r.totalDays} ngày cho học sinh ${r.studentName}.`);
                      }}
                    >
                      <Send size={15} /> Xác nhận & Chuyển Ban Giám Hiệu
                    </button>
                  </div>
                )}

                {/* 3. Branch: Đơn tại Ban Giám Hiệu (Status == UnderReview) */}
                {r.status === 'UnderReview' && (
                  <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, paddingTop: 12, borderTop: '1px solid var(--color-border)', marginTop: 8 }}>
                    {isSchoolBoard || isAdmin ? (
                      <>
                        <button
                          type="button"
                          disabled={processing}
                          className="btn btn--secondary btn--sm"
                          style={{ color: '#e11d48', borderColor: '#fda4af' }}
                          onClick={() => {
                            setRejectingItem(r);
                            setRejectReason('');
                          }}
                        >
                          <X size={15} /> BGH Từ chối
                        </button>
                        <button
                          type="button"
                          disabled={processing}
                          className="btn btn--primary btn--sm"
                          style={{ background: 'var(--color-quaternary)', color: 'var(--color-fg)' }}
                          onClick={() => handleBghApprove(r)}
                        >
                          <Check size={15} /> BGH Phê duyệt
                        </button>
                      </>
                    ) : (
                      <span style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-accent)', fontStyle: 'italic' }}>
                        ⏳ Đơn đã được chuyển lên Ban Giám Hiệu. Đang chờ Ban Giám Hiệu quyết định.
                      </span>
                    )}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}

      {/* REJECT MODAL (GeometricModal) */}
      <GeometricModal
        open={!!rejectingItem}
        onClose={() => setRejectingItem(null)}
        title={isSchoolBoard ? 'Ban Giám Hiệu từ chối đơn' : 'Từ chối đơn xin nghỉ học'}
        badgeText="Xác nhận từ chối"
        badgeColor="pink"
        icon={<AlertTriangle size={24} color="#1E293B" />}
        iconBg="var(--color-secondary-soft)"
        maxWidth="md"
      >
        <div>
          <p style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-fg)', marginBottom: 14 }}>
            Học sinh: <strong>{rejectingItem?.studentName}</strong>. Vui lòng nhập lý do từ chối để hệ thống lưu nhật ký và gửi thông báo tới phụ huynh:
          </p>

          <div className="field" style={{ marginBottom: 16 }}>
            <label className="label" htmlFor="reject-reason">Lý do từ chối *</label>
            <textarea
              id="reject-reason"
              className="input"
              rows={3}
              value={rejectReason}
              onChange={(e) => setRejectReason(e.target.value)}
              placeholder="Ví dụ: Tuần này có bài kiểm tra khảo sát chất lượng, phụ huynh vui lòng cho con đi học đầy đủ..."
              style={{ minHeight: 80, resize: 'vertical' }}
              required
            />
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => setRejectingItem(null)}
            >
              Hủy bỏ
            </button>
            <button
              type="button"
              disabled={processing}
              className="btn btn--primary"
              style={{ background: 'var(--color-secondary)' }}
              onClick={handleConfirmReject}
            >
              <MessageSquare size={16} /> {processing ? 'Đang xử lý...' : 'Xác nhận từ chối'}
            </button>
          </div>
        </div>
      </GeometricModal>

      {/* FORWARD TO SCHOOL BOARD MODAL (GeometricModal) */}
      <GeometricModal
        open={!!forwardingItem}
        onClose={() => setForwardingItem(null)}
        title="Xác nhận & Chuyển Ban Giám Hiệu"
        badgeText="Đơn dài ngày (≥ 3 ngày)"
        badgeColor="violet"
        icon={<Send size={24} color="#1E293B" />}
        iconBg="var(--color-accent-soft)"
        maxWidth="md"
      >
        <div>
          <p style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-fg)', marginBottom: 8 }}>
            Học sinh: <strong>{forwardingItem?.studentName}</strong> (Nghỉ <strong>{forwardingItem?.totalDays} ngày</strong>).
          </p>
          <p style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', marginBottom: 14 }}>
            Theo quy định, đơn xin nghỉ từ 3 ngày trở lên sẽ do Ban Giám Hiệu phê duyệt. Vui lòng ghi ý kiến xác nhận của GVCN để chuyển lên BGH:
          </p>

          <div className="field" style={{ marginBottom: 16 }}>
            <label className="label" htmlFor="forward-notes">Ý kiến của GVCN</label>
            <textarea
              id="forward-notes"
              className="input"
              rows={3}
              value={forwardNotes}
              onChange={(e) => setForwardNotes(e.target.value)}
              placeholder="Nhập ý kiến GVCN (Ví dụ: Đã liên hệ phụ huynh xác nhận học sinh bị ốm điều trị tại viện, kính chuyển BGH phê duyệt)..."
              style={{ minHeight: 80, resize: 'vertical' }}
            />
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => setForwardingItem(null)}
            >
              Hủy bỏ
            </button>
            <button
              type="button"
              disabled={processing}
              className="btn btn--primary"
              onClick={handleConfirmForward}
            >
              <Send size={16} /> {processing ? 'Đang chuyển...' : 'Chuyển Ban Giám Hiệu'}
            </button>
          </div>
        </div>
      </GeometricModal>
    </div>
  );
}
