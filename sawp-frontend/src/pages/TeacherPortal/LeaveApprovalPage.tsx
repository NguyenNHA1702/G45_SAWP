import { useEffect, useState } from 'react';
import { Check, X, ShieldAlert, CheckCircle2, UserCheck, MessageSquare, AlertTriangle, Send, FileText } from 'lucide-react';
import toast from 'react-hot-toast';
import { leaveRequestApi, type LeaveRequestItem } from '../../services/implementations/leaveRequestApi';
import { USER_KEY } from '../../services/apiClient';

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
    <div style={{ padding: '24px', maxWidth: '1200px', margin: '0 auto' }}>
      {/* Header */}
      <div style={{ marginBottom: '24px' }}>
        <h1 style={{ fontSize: '24px', fontWeight: 800, color: '#0F172A', margin: 0 }}>
          {isSchoolBoard ? 'Ban Giám Hiệu - Duyệt đơn xin nghỉ học dài ngày' : 'Tiếp nhận & Duyệt đơn xin nghỉ học'}
        </h1>
        <p style={{ color: '#64748B', marginTop: '4px', fontSize: '14px' }}>
          {isSchoolBoard
            ? 'Phê duyệt các đơn xin nghỉ từ 3 ngày trở lên do Giáo viên chủ nhiệm chuyển tiếp. Tự động đồng bộ điểm danh và suất ăn bán trú.'
            : 'Đơn dưới 3 ngày do GVCN duyệt trực tiếp. Đơn từ 3 ngày trở lên GVCN xác nhận và chuyển Ban Giám Hiệu phê duyệt.'}
        </p>
      </div>

      {/* Filter Tabs */}
      <div style={{ display: 'flex', gap: '8px', marginBottom: '20px', borderBottom: '2px solid #E2E8F0', paddingBottom: '8px', flexWrap: 'wrap' }}>
        {filterTabs.map((tab) => (
          <button
            key={tab.key}
            onClick={() => setStatusFilter(tab.key)}
            style={{
              padding: '8px 18px',
              borderRadius: '8px',
              border: 'none',
              backgroundColor: statusFilter === tab.key ? '#2563EB' : 'transparent',
              color: statusFilter === tab.key ? '#FFFFFF' : '#475569',
              fontWeight: 700,
              fontSize: '14px',
              cursor: 'pointer',
              transition: 'all 0.15s ease',
            }}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* List */}
      {loading ? (
        <div style={{ textAlign: 'center', padding: '40px', color: '#64748B' }}>Đang tải danh sách đơn...</div>
      ) : requests.length === 0 ? (
        <div style={{ textAlign: 'center', padding: '60px 20px', backgroundColor: '#F8FAFC', borderRadius: '16px' }}>
          <UserCheck size={48} color="#94A3B8" style={{ marginBottom: '12px' }} />
          <h3 style={{ margin: 0, color: '#334155' }}>Không có đơn xin nghỉ nào trong mục này</h3>
        </div>
      ) : (
        <div style={{ display: 'grid', gap: '16px' }}>
          {requests.map((r) => {
            const isEscalated = (r.totalDays || 1) >= 3;
            return (
              <div
                key={r.id}
                style={{
                  backgroundColor: '#FFFFFF',
                  borderRadius: '14px',
                  border: r.status === 2 ? '1px solid #C084FC' : '1px solid #E2E8F0',
                  padding: '20px',
                  boxShadow: '0 2px 8px rgba(0,0,0,0.04)',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: '12px',
                }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '8px' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                    <span
                      style={{
                        fontFamily: 'monospace',
                        fontWeight: 700,
                        backgroundColor: '#F1F5F9',
                        padding: '4px 8px',
                        borderRadius: '6px',
                        color: '#475569',
                        fontSize: '13px',
                      }}
                    >
                      {r.requestNumber}
                    </span>
                    <strong style={{ fontSize: '17px', color: '#0F172A' }}>{r.studentName}</strong>
                    <span style={{ color: '#475569', fontSize: '13px' }}>Mã: {r.studentCode}</span>
                    {r.className && (
                      <span style={{ backgroundColor: '#EEF2FF', color: '#4338CA', padding: '2px 8px', borderRadius: '6px', fontSize: '12px', fontWeight: 600 }}>
                        Lớp {r.className}
                      </span>
                    )}
                  </div>

                  <div>
                    {r.status === 1 && (
                      <span style={{ backgroundColor: '#FEF3C7', color: '#B45309', padding: '4px 12px', borderRadius: '20px', fontWeight: 700, fontSize: '12px' }}>
                        {isEscalated ? 'Chờ GV chuyển BGH' : 'Chờ GVCN duyệt'}
                      </span>
                    )}
                    {r.status === 2 && (
                      <span style={{ backgroundColor: '#FAF5FF', color: '#7E22CE', border: '1px solid #E9D5FF', padding: '4px 12px', borderRadius: '20px', fontWeight: 700, fontSize: '12px' }}>
                        Chờ BGH duyệt
                      </span>
                    )}
                    {r.status === 3 && (
                      <span style={{ backgroundColor: '#DCFCE7', color: '#15803D', padding: '4px 12px', borderRadius: '20px', fontWeight: 700, fontSize: '12px', display: 'flex', alignItems: 'center', gap: '4px' }}>
                        <CheckCircle2 size={14} /> Đã phê duyệt
                      </span>
                    )}
                    {r.status === 4 && (
                      <span style={{ backgroundColor: '#FEE2E2', color: '#B91C1C', padding: '4px 12px', borderRadius: '20px', fontWeight: 700, fontSize: '12px', display: 'flex', alignItems: 'center', gap: '4px' }}>
                        <ShieldAlert size={14} /> Đã từ chối
                      </span>
                    )}
                  </div>
                </div>

                {/* Routing Banner on Card */}
                {r.status === 1 && isEscalated && (
                  <div style={{ backgroundColor: '#FAF5FF', border: '1px solid #E9D5FF', padding: '8px 12px', borderRadius: '8px', fontSize: '13px', color: '#6B21A8', display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <ShieldAlert size={16} />
                    <span>Đơn xin nghỉ <strong>{r.totalDays} ngày</strong> (từ 3 ngày trở lên) - GVCN cần xác nhận yêu cầu và chuyển tiếp lên Ban Giám Hiệu.</span>
                  </div>
                )}

                {r.status === 2 && (
                  <div style={{ backgroundColor: '#F3E8FF', border: '1px solid #D8B4FE', padding: '8px 12px', borderRadius: '8px', fontSize: '13px', color: '#581C87', display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <FileText size={16} />
                    <span>Đơn nghỉ chuyển cấp <strong>{r.totalDays} ngày</strong> đã được GVCN xác nhận và đang chờ Ban Giám Hiệu xem xét quyết định.</span>
                  </div>
                )}

                {/* Information Grid */}
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '12px', fontSize: '14px', color: '#334155' }}>
                  <div>
                    <span style={{ color: '#64748B' }}>Phụ huynh gửi:</span>{' '}
                    <strong>{r.requesterName}</strong> {r.requesterPhone && <small>({r.requesterPhone})</small>}
                  </div>
                  <div>
                    <span style={{ color: '#64748B' }}>Thời gian:</span>{' '}
                    <strong>
                      {new Date(r.startDate).toLocaleDateString('vi-VN')} {r.startDate !== r.endDate && `→ ${new Date(r.endDate).toLocaleDateString('vi-VN')}`}
                    </strong>{' '}
                    <span style={{ fontSize: '12px', color: '#64748B' }}>
                      ({r.totalDays || 1} ngày - {isEscalated ? 'Chuyển cấp BGH' : 'Thông thường'})
                    </span>
                  </div>
                  <div>
                    <span style={{ color: '#64748B' }}>Buổi:</span> <strong>{r.sessionName}</strong>
                  </div>
                  <div>
                    <span style={{ color: '#64748B' }}>Suất ăn trưa:</span>{' '}
                    {r.keepLunchService ? (
                      <strong style={{ color: '#0284C7' }}>Phụ huynh chọn GIỮ SUẤT ĂN</strong>
                    ) : (
                      <span>Tự động hủy nếu duyệt trước 08h15</span>
                    )}
                  </div>
                </div>

                {/* Reason & Attachments */}
                <div style={{ backgroundColor: '#F8FAFC', padding: '12px 16px', borderRadius: '8px', fontSize: '14px' }}>
                  <div>
                    <span style={{ color: '#64748B', fontWeight: 600 }}>Lý do phụ huynh trình bày: </span>
                    <span style={{ color: '#0F172A' }}>{r.reason}</span>
                  </div>
                  {r.doctorNoteAttachmentUrl && (
                    <div style={{ marginTop: '6px', fontSize: '13px', color: '#0284C7' }}>
                      📎 <strong>Tài liệu đính kèm:</strong> {r.doctorNoteAttachmentUrl}
                    </div>
                  )}
                  {r.reviewerNotes && (
                    <div style={{ marginTop: '8px', paddingTop: '8px', borderTop: '1px dashed #CBD5E1', color: r.status === 4 ? '#B91C1C' : '#0369A1' }}>
                      <strong>Nhật ký xử lý: </strong>
                      <span>{r.reviewerNotes}</span>
                    </div>
                  )}
                </div>

                {/* ACTION BUTTONS */}
                {/* 1. Branch: Dưới 3 ngày (GVCN xem xét đơn nghỉ thông thường) */}
                {r.status === 1 && !isEscalated && (
                  <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', paddingTop: '8px' }}>
                    <button
                      disabled={processing}
                      onClick={() => {
                        setRejectingItem(r);
                        setRejectReason('');
                      }}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '6px',
                        backgroundColor: '#FFFFFF',
                        color: '#DC2626',
                        border: '1px solid #FCA5A5',
                        padding: '8px 16px',
                        borderRadius: '8px',
                        fontWeight: 600,
                        cursor: 'pointer',
                      }}
                    >
                      <X size={16} /> Từ chối
                    </button>
                    <button
                      disabled={processing}
                      onClick={() => handleApprove(r)}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '6px',
                        backgroundColor: '#16A34A',
                        color: '#FFFFFF',
                        border: 'none',
                        padding: '8px 20px',
                        borderRadius: '8px',
                        fontWeight: 700,
                        cursor: 'pointer',
                        boxShadow: '0 2px 6px rgba(22, 163, 74, 0.25)',
                      }}
                    >
                      <Check size={16} /> Phê duyệt đơn
                    </button>
                  </div>
                )}

                {/* 2. Branch: Từ 3 ngày trở lên (GVCN xác nhận yêu cầu và chuyển lên Ban Giám hiệu) */}
                {r.status === 1 && isEscalated && (
                  <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', paddingTop: '8px' }}>
                    <button
                      disabled={processing}
                      onClick={() => {
                        setRejectingItem(r);
                        setRejectReason('');
                      }}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '6px',
                        backgroundColor: '#FFFFFF',
                        color: '#DC2626',
                        border: '1px solid #FCA5A5',
                        padding: '8px 16px',
                        borderRadius: '8px',
                        fontWeight: 600,
                        cursor: 'pointer',
                      }}
                    >
                      <X size={16} /> Từ chối
                    </button>
                    <button
                      disabled={processing}
                      onClick={() => {
                        setForwardingItem(r);
                        setForwardNotes(`Kính chuyển Ban Giám Hiệu xem xét phê duyệt đơn xin nghỉ ${r.totalDays} ngày cho học sinh ${r.studentName}.`);
                      }}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '6px',
                        backgroundColor: '#7E22CE',
                        color: '#FFFFFF',
                        border: 'none',
                        padding: '8px 20px',
                        borderRadius: '8px',
                        fontWeight: 700,
                        cursor: 'pointer',
                        boxShadow: '0 2px 6px rgba(126, 34, 206, 0.25)',
                      }}
                    >
                      <Send size={16} /> Xác nhận & Chuyển Ban Giám Hiệu
                    </button>
                  </div>
                )}

                {/* 3. Branch: Đơn tại Ban Giám Hiệu (Status == UnderReview) */}
                {r.status === 2 && (
                  <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', paddingTop: '8px' }}>
                    {isSchoolBoard || isAdmin ? (
                      <>
                        <button
                          disabled={processing}
                          onClick={() => {
                            setRejectingItem(r);
                            setRejectReason('');
                          }}
                          style={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: '6px',
                            backgroundColor: '#FFFFFF',
                            color: '#DC2626',
                            border: '1px solid #FCA5A5',
                            padding: '8px 16px',
                            borderRadius: '8px',
                            fontWeight: 600,
                            cursor: 'pointer',
                          }}
                        >
                          <X size={16} /> BGH Từ chối
                        </button>
                        <button
                          disabled={processing}
                          onClick={() => handleBghApprove(r)}
                          style={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: '6px',
                            backgroundColor: '#16A34A',
                            color: '#FFFFFF',
                            border: 'none',
                            padding: '8px 20px',
                            borderRadius: '8px',
                            fontWeight: 700,
                            cursor: 'pointer',
                            boxShadow: '0 2px 6px rgba(22, 163, 74, 0.25)',
                          }}
                        >
                          <Check size={16} /> BGH Phê duyệt
                        </button>
                      </>
                    ) : (
                      <span style={{ fontSize: '13px', color: '#7E22CE', fontStyle: 'italic' }}>
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

      {/* REJECT MODAL */}
      {rejectingItem && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            backgroundColor: 'rgba(15, 23, 42, 0.6)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 1000,
            padding: '16px',
          }}
        >
          <div
            style={{
              backgroundColor: '#FFFFFF',
              borderRadius: '16px',
              maxWidth: '480px',
              width: '100%',
              padding: '24px',
              boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px', color: '#DC2626', marginBottom: '12px' }}>
              <AlertTriangle size={24} />
              <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 700 }}>
                {isSchoolBoard ? 'Ban Giám Hiệu từ chối đơn xin nghỉ' : 'Từ chối đơn xin nghỉ học'}
              </h3>
            </div>
            <p style={{ fontSize: '14px', color: '#475569', marginBottom: '16px' }}>
              Học sinh: <strong>{rejectingItem.studentName}</strong>. Vui lòng nhập lý do từ chối để hệ thống lưu nhật ký và gửi thông báo đến phụ huynh:
            </p>

            <textarea
              rows={3}
              value={rejectReason}
              onChange={(e) => setRejectReason(e.target.value)}
              placeholder="Ví dụ: Tuần này có bài kiểm tra khảo sát chất lượng, phụ huynh vui lòng cho con đi học đầy đủ..."
              style={{
                width: '100%',
                padding: '10px',
                borderRadius: '8px',
                border: '1px solid #CBD5E1',
                fontSize: '14px',
                marginBottom: '16px',
                outline: 'none',
              }}
              required
            />

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
              <button
                type="button"
                onClick={() => setRejectingItem(null)}
                style={{ padding: '8px 16px', borderRadius: '8px', border: '1px solid #CBD5E1', background: '#F8FAFC', cursor: 'pointer', fontWeight: 600 }}
              >
                Hủy bỏ
              </button>
              <button
                type="button"
                disabled={processing}
                onClick={handleConfirmReject}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: '6px',
                  padding: '8px 18px',
                  borderRadius: '8px',
                  border: 'none',
                  backgroundColor: '#DC2626',
                  color: '#FFFFFF',
                  fontWeight: 700,
                  cursor: 'pointer',
                }}
              >
                <MessageSquare size={16} /> Xác nhận từ chối
              </button>
            </div>
          </div>
        </div>
      )}

      {/* FORWARD TO SCHOOL BOARD MODAL */}
      {forwardingItem && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            backgroundColor: 'rgba(15, 23, 42, 0.6)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 1000,
            padding: '16px',
          }}
        >
          <div
            style={{
              backgroundColor: '#FFFFFF',
              borderRadius: '16px',
              maxWidth: '520px',
              width: '100%',
              padding: '24px',
              boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px', color: '#7E22CE', marginBottom: '12px' }}>
              <Send size={24} />
              <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 700 }}>
                Xác nhận & Chuyển Ban Giám Hiệu
              </h3>
            </div>
            <p style={{ fontSize: '14px', color: '#475569', marginBottom: '12px' }}>
              Học sinh: <strong>{forwardingItem.studentName}</strong> (Nghỉ <strong>{forwardingItem.totalDays} ngày</strong>).
            </p>
            <p style={{ fontSize: '13px', color: '#64748B', marginBottom: '14px' }}>
              Theo quy định, đơn xin nghỉ từ 3 ngày trở lên sẽ do Ban Giám Hiệu phê duyệt. Vui lòng ghi ý kiến xác nhận của GVCN để chuyển lên BGH:
            </p>

            <textarea
              rows={3}
              value={forwardNotes}
              onChange={(e) => setForwardNotes(e.target.value)}
              placeholder="Nhập ý kiến GVCN (Ví dụ: Đã liên hệ phụ huynh xác nhận học sinh bị ốm điều trị tại viện, kính chuyển BGH phê duyệt)..."
              style={{
                width: '100%',
                padding: '10px',
                borderRadius: '8px',
                border: '1px solid #CBD5E1',
                fontSize: '14px',
                marginBottom: '16px',
                outline: 'none',
              }}
            />

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
              <button
                type="button"
                onClick={() => setForwardingItem(null)}
                style={{ padding: '8px 16px', borderRadius: '8px', border: '1px solid #CBD5E1', background: '#F8FAFC', cursor: 'pointer', fontWeight: 600 }}
              >
                Hủy bỏ
              </button>
              <button
                type="button"
                disabled={processing}
                onClick={handleConfirmForward}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: '6px',
                  padding: '8px 18px',
                  borderRadius: '8px',
                  border: 'none',
                  backgroundColor: '#7E22CE',
                  color: '#FFFFFF',
                  fontWeight: 700,
                  cursor: 'pointer',
                }}
              >
                <Send size={16} /> Chuyển Ban Giám Hiệu
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
