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
  HelpCircle,
  Clock,
  RotateCcw,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { leaveRequestApi, type LeaveRequestItem } from '../../services/implementations/leaveRequestApi';
import { getAuthUser } from '../../services/apiClient';
import { LUNCH_CUTOFF_LABEL, formatDateDDMMYYYY } from '../../constants/lunch';
import { formatViDateTime } from '../../utils/date';
import { asApiError } from '../../utils/apiError';
import { GeometricModal } from '../../components/common/GeometricModal';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { PaginationBar } from '../../components/common/PaginationBar';

export default function LeaveApprovalPage() {
  const [requests, setRequests] = useState<LeaveRequestItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(15);
  const [loading, setLoading] = useState(true);

  // L-A: Đọc phiên người dùng qua getAuthUser() (sessionStorage, fallback localStorage)
  const userJson = getAuthUser();
  const currentUser = userJson ? JSON.parse(userJson) : null;
  const userRoles: string[] = currentUser?.roles ?? [];
  const isSchoolBoard = userRoles.includes('SchoolBoard');
  const isAdmin = userRoles.includes('Admin');
  const isBoardOrAdmin = isSchoolBoard || isAdmin;

  // Status Filter: default 'UnderReview' for SchoolBoard, 'Submitted' for Teacher
  const [statusFilter, setStatusFilter] = useState<string>(isSchoolBoard ? 'UnderReview' : 'Submitted');

  // Modal Reject state
  const [rejectingItem, setRejectingItem] = useState<LeaveRequestItem | null>(null);
  const [rejectReason, setRejectReason] = useState('');

  // Modal Request More Info state (A10)
  const [moreInfoItem, setMoreInfoItem] = useState<LeaveRequestItem | null>(null);
  const [moreInfoNotes, setMoreInfoNotes] = useState('');

  // Modal Forward to School Board state
  const [forwardingItem, setForwardingItem] = useState<LeaveRequestItem | null>(null);
  const [forwardNotes, setForwardNotes] = useState('');

  // Modal Confirm Approve state (đồng bộ ConfirmDialog thay window.confirm)
  const [approvingTarget, setApprovingTarget] = useState<{ item: LeaveRequestItem; isBgh: boolean } | null>(null);

  const [processing, setProcessing] = useState(false);

  const fetchRequests = async () => {
    try {
      setLoading(true);
      const res = await leaveRequestApi.getClassRequestsPaged({
        status: statusFilter === 'All' ? undefined : statusFilter,
        pageIndex,
        pageSize,
      });
      if (res.success && res.data) {
        setRequests(res.data.items);
        setTotalCount(res.data.totalCount);
      }
    } catch (err: unknown) {
      const apiErr = asApiError(err);
      toast.error(apiErr.errors?.[0] || apiErr.message || 'Không thể tải danh sách đơn xin nghỉ.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchRequests();
  }, [statusFilter, pageIndex, pageSize]);

  // Xác nhận phê duyệt đơn (GVCN <= 3 ngày học, hoặc BGH cho đơn chuyển cấp / lớp chưa có GVCN)
  const handleConfirmApprove = async () => {
    if (!approvingTarget) return;
    const { item, isBgh } = approvingTarget;
    const actorTitle = isBgh || isBoardOrAdmin ? 'Ban Giám Hiệu' : 'GVCN';

    try {
      setProcessing(true);
      const res = await leaveRequestApi.review(item.id, {
        isApproved: true,
        reviewerNotes: isBgh
          ? 'Ban Giám Hiệu đồng ý phê duyệt đơn xin nghỉ học.'
          : isBoardOrAdmin
            ? 'Ban Giám Hiệu đồng ý cho học sinh nghỉ học.'
            : 'GVCN đồng ý cho học sinh nghỉ học.',
      });

      if (res.success) {
        toast.success(`${actorTitle} đã phê duyệt đơn thành công! ${res.data.mealSyncMessage}`, { duration: 5000 });
        if (res.data.attendanceWarnings && res.data.attendanceWarnings.length > 0) {
          toast(res.data.attendanceWarnings.join(' '), { icon: '⚠️', duration: 7000 });
        }
        fetchRequests();
      } else {
        toast.error(res.message);
      }
    } catch (err: unknown) {
      const apiErr = asApiError(err);
      toast.error(apiErr.errors?.[0] || apiErr.message);
    } finally {
      setProcessing(false);
      setApprovingTarget(null);
    }
  };

  // Yêu cầu phụ huynh bổ sung thông tin (A10 + L-C)
  const handleConfirmRequestMoreInfo = async () => {
    if (!moreInfoItem) return;
    const trimmed = moreInfoNotes.trim();
    if (trimmed.length < 5 || trimmed.length > 500) {
      toast.error('Nội dung yêu cầu bổ sung bắt buộc từ 5 đến 500 ký tự.');
      return;
    }

    try {
      setProcessing(true);
      const res = await leaveRequestApi.review(moreInfoItem.id, {
        isApproved: false,
        isRequestMoreInfo: true,
        reviewerNotes: trimmed,
      });

      if (res.success) {
        toast.success(
          isBoardOrAdmin
            ? 'Ban Giám Hiệu đã gửi yêu cầu bổ sung thông tin tới phụ huynh.'
            : 'Đã gửi yêu cầu bổ sung thông tin tới phụ huynh.'
        );
        setMoreInfoItem(null);
        setMoreInfoNotes('');
        fetchRequests();
      } else {
        toast.error(res.message);
      }
    } catch (err: unknown) {
      const apiErr = asApiError(err);
      toast.error(apiErr.errors?.[0] || apiErr.message);
    } finally {
      setProcessing(false);
    }
  };

  // Xác nhận từ chối đơn (GVCN hoặc BGH — L-A + L-C)
  const handleConfirmReject = async () => {
    if (!rejectingItem) return;
    const trimmed = rejectReason.trim();
    if (trimmed.length < 5 || trimmed.length > 500) {
      toast.error('Lý do từ chối bắt buộc từ 5 đến 500 ký tự.');
      return;
    }

    try {
      setProcessing(true);
      const res = await leaveRequestApi.review(rejectingItem.id, {
        isApproved: false,
        reviewerNotes: trimmed,
      });

      if (res.success) {
        toast.success(
          isBoardOrAdmin
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
      const apiErr = asApiError(err);
      toast.error(apiErr.errors?.[0] || apiErr.message);
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
      const apiErr = asApiError(err);
      toast.error(apiErr.errors?.[0] || apiErr.message);
    } finally {
      setProcessing(false);
    }
  };

  // Danh sách các tab theo vai trò
  const filterTabs = isSchoolBoard
    ? [
        { key: 'UnderReview', label: 'Chờ BGH duyệt' },
        { key: 'NeedMoreInfo', label: 'Chờ PH bổ sung' },
        { key: 'Approved', label: 'Đã phê duyệt' },
        { key: 'Rejected', label: 'Đã từ chối' },
        { key: 'All', label: 'Tất cả' },
      ]
    : [
        { key: 'Submitted', label: 'Chờ GVCN xử lý' },
        { key: 'NeedMoreInfo', label: 'Chờ PH bổ sung' },
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
                ? 'Phê duyệt các đơn xin nghỉ trên 3 ngày học (> 3 ngày) do Giáo viên chủ nhiệm chuyển tiếp và các đơn từ lớp chưa có GVCN. Tự động đồng bộ điểm danh và suất ăn bán trú.'
                : 'Đơn từ 1 đến 3 ngày học do GVCN duyệt trực tiếp. Đơn trên 3 ngày học (> 3 ngày) GVCN xác nhận và chuyển Ban Giám Hiệu phê duyệt.'}
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
              onClick={() => {
                setStatusFilter(tab.key);
                setPageIndex(1);
              }}
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
            const isEscalated = Boolean(r.isEscalated);
            const noHomeroom = r.hasHomeroomTeacher === false;
            return (
              <div
                key={r.id}
                className="card card--hover"
                style={{
                  border: isEscalated || noHomeroom ? '2px solid var(--color-accent)' : 'var(--border)',
                  boxShadow: isEscalated || noHomeroom ? '8px 8px 0 0 var(--color-accent-soft)' : 'var(--shadow-card)',
                }}
              >
                {/* Header card */}
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 10, marginBottom: 12 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
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
                      <span className={noHomeroom ? 'badge badge--violet' : 'badge badge--amber'}>
                        {noHomeroom
                          ? 'Chờ BGH duyệt (lớp chưa có GVCN)'
                          : isEscalated
                            ? 'Chờ GVCN chuyển BGH'
                            : 'Chờ GVCN duyệt'}
                      </span>
                    )}
                    {r.status === 'UnderReview' && (
                      <span className="badge badge--violet">
                        Chờ BGH duyệt
                      </span>
                    )}
                    {r.status === 'NeedMoreInfo' && (
                      <span className="badge badge--pink">
                        <HelpCircle size={14} /> Chờ PH bổ sung
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
                      r.isWithdrawn ? (
                        <span className="badge badge--amber">
                          <RotateCcw size={13} /> Đã thu hồi
                        </span>
                      ) : (
                        <span className="badge badge--muted">
                          Đã hủy
                        </span>
                      )
                    )}
                  </div>
                </div>

                {/* Routing Banner on Card */}
                {r.status === 'Submitted' && noHomeroom && (
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
                      {r.className ? `Lớp ${r.className}` : 'Lớp của học sinh'} hiện <strong>chưa có GVCN</strong> — Đơn được chuyển thẳng tới Ban Giám Hiệu xem xét phê duyệt.
                    </span>
                  </div>
                )}

                {r.status === 'Submitted' && isEscalated && !noHomeroom && (
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
                      Đơn xin nghỉ <strong>{r.totalDays} ngày học</strong> (&gt; 3 ngày học) — GVCN cần xác nhận thông tin và chuyển tiếp lên Ban Giám Hiệu.
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
                      Đơn nghỉ chuyển cấp <strong>{r.totalDays} ngày học</strong> đã được GVCN xác nhận và đang chờ Ban Giám Hiệu phê duyệt.
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
                      {formatDateDDMMYYYY(r.startDate)} {r.startDate !== r.endDate && `→ ${formatDateDDMMYYYY(r.endDate)}`}
                    </strong>{' '}
                    <span className={`badge ${isEscalated ? 'badge--violet' : 'badge--mint'}`} style={{ fontSize: 'var(--fs-xs)', marginLeft: 4 }}>
                      {r.totalDays} ngày học
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
                    <div style={{ marginTop: 8, display: 'flex', flexWrap: 'wrap', gap: 8 }}>
                      {(() => {
                        const urls = r.doctorNoteAttachmentUrl
                          .split('|')
                          .map((u) => u.trim())
                          .filter(Boolean);
                        return urls.map((urlItem, idx) =>
                          urlItem.startsWith('/') || urlItem.startsWith('http') ? (
                            <button
                              key={idx}
                              type="button"
                              onClick={async () => {
                                try {
                                  await leaveRequestApi.openAttachment(urlItem);
                                } catch {
                                  toast.error('Không thể mở tệp minh chứng của phụ huynh.');
                                }
                              }}
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
                              <span>{urls.length > 1 ? `Xem minh chứng của phụ huynh (${idx + 1})` : 'Xem minh chứng của phụ huynh'}</span>
                              <ExternalLink size={12} />
                            </button>
                          ) : (
                            <div key={idx} style={{ display: 'inline-flex', alignItems: 'center', gap: 6, color: 'var(--color-accent)' }}>
                              <Paperclip size={14} />
                              <strong>Minh chứng:</strong> {urlItem}
                            </div>
                          )
                        );
                      })()}
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

                  {r.statusHistory && r.statusHistory.length > 0 && (
                    <div style={{ marginTop: 10, paddingTop: 8, borderTop: '1px dashed var(--color-border)' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontWeight: 800, fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', marginBottom: 4 }}>
                        <Clock size={12} /> Lịch sử trạng thái:
                      </div>
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                        {r.statusHistory.map((h, idx) => (
                          <div key={idx} style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-fg)' }}>
                            <strong>{h.actionLabel}</strong>
                            {h.actorName ? ` (${h.actorName})` : ''}
                            {' — '}
                            <span style={{ color: 'var(--color-muted-fg)' }}>{formatViDateTime(h.timestamp)}</span>
                            {h.notes ? `: ${h.notes}` : ''}
                          </div>
                        ))}
                      </div>
                    </div>
                  )}
                </div>

                {/* ACTION BUTTONS */}
                {/* 1. Branch: <= 3 ngày học (GVCN xem xét đơn nghỉ thông thường) */}
                {r.status === 'Submitted' && !isEscalated && !noHomeroom && !isBoardOrAdmin && (
                  <div style={{ display: 'flex', justifyContent: 'flex-end', flexWrap: 'wrap', gap: 10, paddingTop: 12, borderTop: '1px solid var(--color-border)', marginTop: 8 }}>
                    <button
                      type="button"
                      disabled={processing}
                      className="btn btn--secondary btn--sm"
                      onClick={() => {
                        setMoreInfoItem(r);
                        setMoreInfoNotes('');
                      }}
                    >
                      <HelpCircle size={15} /> Yêu cầu bổ sung
                    </button>
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
                      onClick={() => setApprovingTarget({ item: r, isBgh: false })}
                    >
                      <Check size={15} /> Phê duyệt đơn
                    </button>
                  </div>
                )}

                {/* 2. Branch: > 3 ngày học (GVCN xác nhận yêu cầu và chuyển lên Ban Giám hiệu; không được từ chối thay BGH) */}
                {r.status === 'Submitted' && isEscalated && !noHomeroom && !isBoardOrAdmin && (
                  <div style={{ display: 'flex', justifyContent: 'flex-end', flexWrap: 'wrap', gap: 10, paddingTop: 12, borderTop: '1px solid var(--color-border)', marginTop: 8 }}>
                    <button
                      type="button"
                      disabled={processing}
                      className="btn btn--secondary btn--sm"
                      onClick={() => {
                        setMoreInfoItem(r);
                        setMoreInfoNotes('');
                      }}
                    >
                      <HelpCircle size={15} /> Yêu cầu bổ sung
                    </button>
                    <button
                      type="button"
                      disabled={processing}
                      className="btn btn--primary btn--sm"
                      onClick={() => {
                        setForwardingItem(r);
                        setForwardNotes(`Kính chuyển Ban Giám Hiệu xem xét phê duyệt đơn xin nghỉ ${r.totalDays} ngày học cho học sinh ${r.studentName}.`);
                      }}
                    >
                      <Send size={15} /> Xác nhận & Chuyển Ban Giám Hiệu
                    </button>
                  </div>
                )}

                {/* 3. Branch: Đơn tại Ban Giám Hiệu (Status == UnderReview hoặc lớp chưa có GVCN) */}
                {(r.status === 'UnderReview' || (r.status === 'Submitted' && noHomeroom)) && (
                  <div style={{ display: 'flex', justifyContent: 'flex-end', flexWrap: 'wrap', gap: 10, paddingTop: 12, borderTop: '1px solid var(--color-border)', marginTop: 8 }}>
                    {isSchoolBoard ? (
                      <>
                        <button
                          type="button"
                          disabled={processing}
                          className="btn btn--secondary btn--sm"
                          onClick={() => {
                            setMoreInfoItem(r);
                            setMoreInfoNotes('');
                          }}
                        >
                          <HelpCircle size={15} /> Yêu cầu bổ sung
                        </button>
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
                          onClick={() => setApprovingTarget({ item: r, isBgh: true })}
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

      <PaginationBar
        pageIndex={pageIndex}
        pageSize={pageSize}
        totalCount={totalCount}
        onPageChange={setPageIndex}
        onPageSizeChange={(s) => {
          setPageSize(s);
          setPageIndex(1);
        }}
        itemLabel="đơn xin nghỉ"
      />

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
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <label className="label" htmlFor="reject-reason">Lý do từ chối (5–500 ký tự) *</label>
              <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>{rejectReason.trim().length}/500</span>
            </div>
            <textarea
              id="reject-reason"
              className="input"
              rows={3}
              minLength={5}
              maxLength={500}
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
        badgeText="Đơn dài ngày (> 3 ngày học)"
        badgeColor="violet"
        icon={<Send size={24} color="#1E293B" />}
        iconBg="var(--color-accent-soft)"
        maxWidth="md"
      >
        <div>
          <p style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-fg)', marginBottom: 8 }}>
            Học sinh: <strong>{forwardingItem?.studentName}</strong> (Nghỉ <strong>{forwardingItem?.totalDays} ngày học</strong>).
          </p>
          <p style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', marginBottom: 14 }}>
            Theo quy định, đơn xin nghỉ trên 3 ngày học (&gt; 3 ngày) sẽ do Ban Giám Hiệu phê duyệt. Vui lòng ghi ý kiến xác nhận của GVCN để chuyển lên BGH:
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

      {/* REQUEST MORE INFO MODAL (A10) */}
      <GeometricModal
        open={!!moreInfoItem}
        onClose={() => setMoreInfoItem(null)}
        title="Yêu cầu phụ huynh bổ sung thông tin"
        badgeText="Trả đơn yêu cầu bổ sung"
        badgeColor="pink"
        icon={<HelpCircle size={24} color="#1E293B" />}
        iconBg="var(--color-tertiary-soft)"
        maxWidth="md"
      >
        <div>
          <p style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-fg)', marginBottom: 12 }}>
            Học sinh: <strong>{moreInfoItem?.studentName}</strong> ({moreInfoItem?.requestNumber}). Nhập nội dung cần phụ huynh bổ sung (VD: bổ sung giấy khám bệnh, làm rõ lý do nghỉ...):
          </p>

          <div className="field" style={{ marginBottom: 16 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <label className="label" htmlFor="more-info-notes">Nội dung yêu cầu bổ sung (5–500 ký tự) *</label>
              <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>{moreInfoNotes.trim().length}/500</span>
            </div>
            <textarea
              id="more-info-notes"
              className="input"
              rows={3}
              minLength={5}
              maxLength={500}
              value={moreInfoNotes}
              onChange={(e) => setMoreInfoNotes(e.target.value)}
              placeholder="Ví dụ: Phụ huynh vui lòng đính kèm giấy khám bệnh hoặc xác nhận của bác sĩ..."
              style={{ minHeight: 80, resize: 'vertical' }}
              required
            />
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => setMoreInfoItem(null)}
            >
              Hủy bỏ
            </button>
            <button
              type="button"
              disabled={processing}
              className="btn btn--primary"
              onClick={handleConfirmRequestMoreInfo}
            >
              <Send size={16} /> {processing ? 'Đang gửi...' : 'Gửi yêu cầu bổ sung'}
            </button>
          </div>
        </div>
      </GeometricModal>

      {/* APPROVE CONFIRM DIALOG */}
      <ConfirmDialog
        open={!!approvingTarget}
        title={approvingTarget?.isBgh || isBoardOrAdmin ? 'Ban Giám Hiệu xác nhận phê duyệt' : 'GVCN xác nhận phê duyệt'}
        confirmText={processing ? 'Đang xử lý...' : 'Xác nhận phê duyệt'}
        cancelText="Hủy bỏ"
        busy={processing}
        onConfirm={handleConfirmApprove}
        onCancel={() => {
          if (!processing) setApprovingTarget(null);
        }}
      >
        {approvingTarget && (
          <p style={{ margin: 0, lineHeight: 1.6 }}>
            {approvingTarget.isBgh || isBoardOrAdmin ? 'Ban Giám Hiệu' : 'GVCN'} xác nhận <strong>PHÊ DUYỆT</strong> đơn{' '}
            <strong>{approvingTarget.item.requestNumber}</strong> xin nghỉ{' '}
            <strong>{approvingTarget.item.totalDays} ngày học</strong> (
            <strong>
              {formatDateDDMMYYYY(approvingTarget.item.startDate)}
              {approvingTarget.item.startDate.slice(0, 10) !== approvingTarget.item.endDate.slice(0, 10)
                ? ` → ${formatDateDDMMYYYY(approvingTarget.item.endDate)}`
                : ''}
            </strong>
            ) cho học sinh <strong>{approvingTarget.item.studentName}</strong>?
          </p>
        )}
      </ConfirmDialog>
    </div>
  );
}
